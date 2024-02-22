// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using GitIgnore = Ignore.Ignore;

namespace MartinCostello.DotNetBumper.PostProcessors;

internal sealed partial class RemainingTargetFrameworksPostProcessor(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<RemainingTargetFrameworksPostProcessor> logger) : PostProcessor(console, options, logger)
{
    protected override string Action => "Running tests";

    protected override string InitialStatus => "Test project";

    protected override async Task<ProcessingResult> PostProcessCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var gitignore = await LoadGitIgnoreAsync(cancellationToken);

        var references = new Dictionary<FileWithPotentialEdits, List<PotentialFileEdit>>();

        foreach (var path in Directory.EnumerateFiles(Options.ProjectPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = RelativeName(path).Replace('\\', '/');

            if (gitignore?.IsIgnored(relativePath) is true)
            {
                continue;
            }

            int lineNumber = 0;
            var fileReferences = new List<PotentialFileEdit>();

            foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
            {
                lineNumber++;

                IList<Match> matches = line.MatchTargetFrameworkMonikers();

                if (matches.Count is not 0)
                {
                    foreach (var match in matches)
                    {
                        if (match.ValueSpan.ToVersionFromTargetFramework() is { } version && version < upgrade.Channel)
                        {
                            fileReferences.Add(new(lineNumber, match.Index + 1, match.Value));
                        }
                    }
                }
            }

            if (fileReferences.Count > 0)
            {
                references[new(path, relativePath)] = fileReferences;
            }
        }

        if (references.Count > 0)
        {
            var table = new Table
            {
                Title = new TableTitle("Remaining Target Framework References"),
            };

            table.AddColumn("Location");
            table.AddColumn(new TableColumn("Match").RightAligned());

            foreach ((var file, var values) in references.OrderBy((p) => p.Key.RelativePath))
            {
                foreach (var item in values)
                {
                    table.AddRow(Location(file, item), Match(item.Text));
                }
            }

            Console.Write(table);
        }

        return ProcessingResult.Success;

        static Markup Location(FileWithPotentialEdits file, PotentialFileEdit edit)
        {
            string location = VisualStudioCodeLink(file, edit);
            string path = $"{file.RelativePath.EscapeMarkup()}:{edit.Line}";

            return new Markup($"[link={location}]{path}[/]");
        }

        static Markup Match(string text) => new($"[{Color.Yellow}]{text.EscapeMarkup()}[/]");

        static string VisualStudioCodeLink(FileWithPotentialEdits file, PotentialFileEdit? edit = null)
        {
            // See https://code.visualstudio.com/docs/editor/command-line#_opening-vs-code-with-urls
            var builder = new StringBuilder("vscode://file/")
                .Append(file.Path.Replace('\\', '/').EscapeMarkup());

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
        var gitDirectory = FileHelpers.FindDirectoryInProject(Options.ProjectPath, ".git");

        if (gitDirectory is null)
        {
            return null;
        }

        var gitignore = Path.GetFullPath(Path.Combine(gitDirectory, "..", ".gitignore"));

        if (!File.Exists(gitignore))
        {
            return null;
        }

        var ignore = new GitIgnore();
        ignore.Add(".git");

        foreach (var entry in await File.ReadAllLinesAsync(gitignore, cancellationToken))
        {
            ignore.Add(entry);
        }

        return ignore;
    }

    private sealed record FileWithPotentialEdits(string Path, string RelativePath);

    private sealed record PotentialFileEdit(int Line, int Column, string Text);
}
