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

            var result = await dotnet.RunAsync(
                project,
                ["test", "--nologo", "--verbosity", "quiet"],
                cancellationToken);

            if (!result.Success)
            {
                return result;
            }
        }

        return new(true, 0, string.Empty, string.Empty);
    }
}
