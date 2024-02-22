// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.PostProcessors;

internal sealed partial class DotNetTestPostProcessor(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<DotNetTestPostProcessor> logger) : PostProcessor(console, options, logger)
{
    protected override string Action => "Running tests";

    protected override string InitialStatus => "Test project";

    protected override Style? SpinnerStyle { get; } = Style.Parse("green");

    protected override string StatusColor => "teal";

    public override async Task<ProcessingResult> PostProcessAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        if (!Options.TestUpgrade)
        {
            return ProcessingResult.None;
        }

        return await base.PostProcessAsync(upgrade, cancellationToken);
    }

    protected override async Task<ProcessingResult> PostProcessCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var projects = ProjectHelpers.FindProjects(Options.ProjectPath);

        if (projects.Count is 0)
        {
            Console.WriteWarningLine("Could not find any test projects.");
            Console.WriteWarningLine("The project may not be in a working state.");

            return ProcessingResult.Warning;
        }
        else
        {
            var result = await RunTestsAsync(projects, context, cancellationToken);

            Console.WriteLine();

            if (result.Success)
            {
                Console.WriteSuccessLine("Upgrade successfully tested.");
            }
            else
            {
                Console.WriteWarningLine("The project upgrade did not result in a successful test run.");
                Console.WriteWarningLine("The project may not be in a working state.");

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    Console.WriteLine();
                    Console.WriteProgressLine(result.StandardError);
                }

                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    Console.WriteLine();
                    Console.WriteProgressLine(result.StandardOutput);
                }

                if (result.LogEntries.Count > 0)
                {
                    WriteErrorsAndWarnings(result.LogEntries);
                }
            }

            return result.Success ? ProcessingResult.Success : ProcessingResult.Warning;
        }
    }

    private async Task<DotNetResult> RunTestsAsync(
        IReadOnlyList<string> projects,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        foreach (var project in projects)
        {
            string name = ProjectHelpers.RelativeName(Options.ProjectPath, project);
            context.Status = StatusMessage($"Running tests for {name}...");

            var result = await dotnet.RunWithLoggerAsync(
                project,
                ["test", "--nologo", "--verbosity", "quiet"],
                cancellationToken);

            if (!result.Success)
            {
                return result;
            }
        }

        return new(true, 0, string.Empty, string.Empty, []);
    }

    private void WriteErrorsAndWarnings(IList<BumperLogEntry> logEntries)
    {
        var table = new Table
        {
            Title = new TableTitle($"Errors and warnings"),
        };

        table.AddColumn("[bold]Type[/]");
        table.AddColumn("[bold]Id[/]");
        table.AddColumn("[bold]Count[/]");

        foreach (var group in logEntries.GroupBy((p) => p.Type))
        {
            var color = group.Key switch
            {
                "Error" => Color.Red,
                "Warning" => Color.Yellow,
                _ => Color.Blue,
            };

            var typeEscaped = group.Key.EscapeMarkup();
            var type = new Markup($"[{color}]{typeEscaped}[/]");

            foreach (var entries in group.GroupBy((p) => p.Id))
            {
                var helpLink = entries
                    .Where((p) => !string.IsNullOrWhiteSpace(p.HelpLink))
                    .Select((p) => p.HelpLink)
                    .FirstOrDefault();

                string idMarkup = entries.Key.EscapeMarkup();

                if (!string.IsNullOrEmpty(helpLink))
                {
                    string linkEscaped = helpLink.EscapeMarkup();
                    idMarkup = $"[link={linkEscaped}]{idMarkup}[/]";
                }

                var id = new Markup(idMarkup);
                var count = new Markup(entries.Count().ToString(CultureInfo.CurrentCulture)).RightJustified();

                table.AddRow(type, id, count);
            }
        }

        Console.Write(table);
        Console.WriteLine();
    }
}
