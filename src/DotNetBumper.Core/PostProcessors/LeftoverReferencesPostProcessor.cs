// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using GitIgnore = Ignore.Ignore;

namespace MartinCostello.DotNetBumper.PostProcessors;

internal sealed partial class LeftoverReferencesPostProcessor(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<LeftoverReferencesPostProcessor> logger) : PostProcessor(console, options, logger)
{
    protected override string Action => "Find leftover references";

    protected override string InitialStatus => "Search files";

    internal static async Task<List<PotentialFileEdit>> FindReferencesAsync(
        ProjectFile file,
        Version channel,
        CancellationToken cancellationToken)
    {
        int lineNumber = 0;
        var result = new List<PotentialFileEdit>();

        foreach (var line in await File.ReadAllLinesAsync(file.FullPath, cancellationToken))
        {
            lineNumber++;

            IList<Match> matches = line.MatchTargetFrameworkMonikers();

            foreach (var match in matches)
            {
                if (match.ValueSpan.ToVersionFromTargetFramework() is { } version && version < channel)
                {
                    result.Add(new(lineNumber, match.Index + 1, match.Value));
                }
            }
        }

        return result;
    }

    internal async IAsyncEnumerable<ProjectFile> EnumerateProjectFilesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var gitignore = await LoadGitIgnoreAsync(cancellationToken);

        foreach (var path in Directory.EnumerateFiles(Options.ProjectPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = RelativeName(path).Replace('\\', '/');

            if (gitignore?.IsIgnored(relativePath) is true)
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
            RenderTable(Console, references);
        }

        return ProcessingResult.Success;
    }

    private static void RenderTable(
        IAnsiConsole console,
        Dictionary<ProjectFile, List<PotentialFileEdit>> references)
    {
        var table = new Table
        {
            Title = new TableTitle($"Remaining References - {references.Sum((p) => p.Value.Count)}"),
        };

        table.AddColumn("[bold]Location[/]");
        table.AddColumn("[bold]Match[/]");

        foreach ((var file, var values) in references.OrderBy((p) => p.Key.RelativePath))
        {
            foreach (var item in values)
            {
                table.AddRow(Location(file, item), Match(item.Text));
            }
        }

        console.WriteLine();
        console.Write(table);

        static Markup Location(ProjectFile file, PotentialFileEdit edit)
        {
            string location = VisualStudioCodeLink(file, edit);
            string path = $"{file.RelativePath.EscapeMarkup()}:{edit.Line}";

            return new Markup($"[link={location}]{path}[/]");
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
        const string GitDirectory = ".git";
        const string GitIgnoreFile = ".gitignore";

        var gitDirectory = FileHelpers.FindDirectoryInProject(Options.ProjectPath, GitDirectory);

        if (gitDirectory is null)
        {
            return null;
        }

        var gitignore = Path.GetFullPath(Path.Combine(gitDirectory, "..", GitIgnoreFile));

        if (!File.Exists(gitignore))
        {
            return null;
        }

        var ignore = new GitIgnore();
        ignore.Add(GitDirectory);

        foreach (var rule in await File.ReadAllLinesAsync(gitignore, cancellationToken))
        {
            ignore.Add(rule);
        }

        return ignore;
    }
}
