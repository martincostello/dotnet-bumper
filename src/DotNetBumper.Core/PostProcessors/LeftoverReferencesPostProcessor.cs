// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using GitIgnore = Ignore.Ignore;

namespace MartinCostello.DotNetBumper.PostProcessors;

internal sealed partial class LeftoverReferencesPostProcessor(
    IAnsiConsole console,
    IEnvironment environment,
    BumperConfigurationProvider configurationProvider,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<LeftoverReferencesPostProcessor> logger) : PostProcessor(console, environment, options, logger)
{
    static LeftoverReferencesPostProcessor()
    {
        MimeTypes.FallbackMimeType = string.Empty;
    }

    protected override string Action => "Find leftover references";

    protected override string InitialStatus => "Search files";

    internal static async Task<List<PotentialFileEdit>> FindReferencesAsync(
        ProjectFile file,
        Version channel,
        CancellationToken cancellationToken)
    {
        int lineNumber = 0;
        var result = new List<PotentialFileEdit>();
        var expectedTfm = channel.ToTargetFramework();

        foreach (var line in await File.ReadAllLinesAsync(file.FullPath, cancellationToken))
        {
            lineNumber++;

            IList<Match> matches = [];

            if (channel >= DotNetVersions.EightPointZero)
            {
                matches = RuntimeIdentifier.Match(line);

                foreach (var match in matches)
                {
                    if (RuntimeIdentifier.TryParse(match.Value, out var rid) && !rid.IsPortable)
                    {
                        result.Add(PotentialFileEdit.FromMatch(match, lineNumber));
                    }
                }
            }

            if (line.Contains(expectedTfm, StringComparison.Ordinal))
            {
                continue;
            }

            matches = line.MatchTargetFrameworkMonikers();

            foreach (var match in matches)
            {
                if (match.ValueSpan.ToVersionFromTargetFramework() is { } version && version < channel)
                {
                    result.Add(PotentialFileEdit.FromMatch(match, lineNumber));
                }
            }
        }

        return [..result.OrderBy((p) => p.Line).ThenBy((p) => p.Column)];
    }

    internal async IAsyncEnumerable<ProjectFile> EnumerateProjectFilesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var gitignore = await LoadGitIgnoreAsync(cancellationToken);

        foreach (var path in Directory.EnumerateFiles(Options.ProjectPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = NormalizePath(RelativeName(path));

            if (gitignore?.IsIgnored(relativePath) is true || IsIgnoredFileType(path))
            {
                continue;
            }

            yield return new(path, relativePath);
        }
    }

    protected override async Task<ProcessingResult> PostProcessCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var references = new Dictionary<ProjectFile, List<PotentialFileEdit>>();

        await foreach (var file in EnumerateProjectFilesAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            context.Status = StatusMessage($"Searching {file.RelativePath}...");

            var fileReferences = await FindReferencesAsync(file, upgrade.Channel, cancellationToken);

            if (fileReferences.Count > 0)
            {
                references[file] = fileReferences;
            }
        }

        if (references.Count > 0)
        {
            RenderTable(references);
        }

        return ProcessingResult.Success;
    }

    private static bool IsIgnoredFileType(string path)
    {
        if (!MimeTypes.TryGetMimeType(path, out var mimeType))
        {
            return false;
        }

        var segments = mimeType.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 1 && segments[0] switch
        {
            "application" or "font" or "image" or "video" => true,
            _ => false,
        };
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private void RenderTable(Dictionary<ProjectFile, List<PotentialFileEdit>> references)
    {
        logContext.RemainingReferences = references;

        var table = new Table
        {
            Title = new TableTitle($"Remaining References - {references.Sum((p) => p.Value.Count)}"),
        };

        table.AddColumn("[bold]Location[/]");
        table.AddColumn("[bold]Match[/]");

        foreach ((var file, var values) in references.OrderBy((p) => p.Key.RelativePath))
        {
            foreach (var item in values.OrderBy((p) => p.Line).ThenBy((p) => p.Column))
            {
                table.AddRow(Location(file, item, TaskEnvironment), Match(item.Text));
            }
        }

        Console.WriteLine();
        Console.Write(table);

        static Markup Location(ProjectFile file, PotentialFileEdit edit, IEnvironment environment)
        {
            string path = $"{file.RelativePath.EscapeMarkup()}:{edit.Line}";

            if (environment.SupportsLinks)
            {
                string location = VisualStudioCodeLink(file, edit);
                path = $"[link={location}]{path}[/]";
            }

            return new Markup(path);
        }

        static Markup Match(string text) => new($"[{Color.Yellow}]{text.EscapeMarkup()}[/]");

        static string VisualStudioCodeLink(ProjectFile file, PotentialFileEdit? edit = null)
        {
            // See https://code.visualstudio.com/docs/editor/command-line#_opening-vs-code-with-urls
            var builder = new StringBuilder("vscode://file/")
                .Append(file.FullPath.Replace('\\', '/').EscapeMarkup());

            if (edit is not null)
            {
                builder.Append(':')
                       .Append(edit.Line)
                       .Append(':')
                       .Append(edit.Column);
            }

            return builder.ToString();
        }
    }

    private async Task<GitIgnore?> LoadGitIgnoreAsync(CancellationToken cancellationToken)
    {
        var ignore = new GitIgnore();
        var config = await configurationProvider.GetAsync(cancellationToken);

        if (config.RemainingReferencesIgnore.Count > 0)
        {
            ignore.Add(config.RemainingReferencesIgnore.Select(NormalizePath));
        }

        const string GitDirectory = ".git";
        const string GitIgnoreFile = ".gitignore";

        var gitDirectory = FileHelpers.FindDirectoryInProject(Options.ProjectPath, GitDirectory);

        if (gitDirectory is not null)
        {
            var gitignore = Path.GetFullPath(Path.Combine(gitDirectory, "..", GitIgnoreFile));

            if (File.Exists(gitignore))
            {
                ignore.Add(GitDirectory);

                foreach (var rule in await File.ReadAllLinesAsync(gitignore, cancellationToken))
                {
                    ignore.Add(rule);
                }
            }
        }

        return ignore.OriginalRules.Count is 0 ? null : ignore;
    }
}
