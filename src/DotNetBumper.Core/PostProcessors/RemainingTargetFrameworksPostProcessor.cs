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

        var references = new Dictionary<(Uri Location, string RelativePath), List<(string Text, int Line)>>();

        foreach (var path in Directory.EnumerateFiles(Options.ProjectPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = RelativeName(path).Replace('\\', '/');

            if (gitignore?.IsIgnored(relativePath) is true)
            {
                continue;
            }

            int lineNumber = 0;
            var fileReferences = new List<(string Text, int Line)>();

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
                            fileReferences.Add((match.Value, lineNumber));
                        }
                    }
                }
            }

            if (fileReferences.Count > 0)
            {
                references[(new(path), relativePath)] = fileReferences;
            }
        }

        if (references.Count > 0)
        {
            var table = new Table
            {
                Title = new TableTitle("Remaining Target Framework References"),
            };

            table.AddColumn("Path");
            table.AddColumn(new TableColumn("Line").RightAligned());
            table.AddColumn(new TableColumn("Match").RightAligned());

            foreach ((var key, var value) in references.OrderBy((p) => p.Key.RelativePath))
            {
                var first = value[0];
                table.AddRow(Path(key.Location, key.RelativePath), Line(first.Line), Match(first.Text));

                foreach (var item in value.Skip(1))
                {
                    table.AddRow(Empty(), Line(item.Line), Match(item.Text));
                }
            }

            Console.Write(table);
        }

        return ProcessingResult.Success;

        static Markup Empty() => new(string.Empty);
        static Markup Line(int line) => new(line.ToString(CultureInfo.CurrentCulture));
        static Markup Match(string text) => new($"[{Color.Yellow}]{text}[/]");

        static Markup Path(Uri location, string relativePath)
        {
            string locationText = location.ToString().EscapeMarkup();
            string pathText = relativePath.EscapeMarkup();

            return new Markup($"[link={locationText}]{pathText}[/]");
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
}
