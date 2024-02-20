﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that upgrades a project  to a newer version of .NET.
/// </summary>
/// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
/// <param name="dotnet">The <see cref="DotNetProcess"/> to use.</param>
/// <param name="upgradeFinder">The <see cref="DotNetUpgradeFinder"/> to use.</param>
/// <param name="upgraders">The <see cref="IUpgrader"/> implementations to use.</param>
/// <param name="timeProvider">The <see cref="TimeProvider"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{ProjectUpgrader}"/> to use.</param>
public partial class ProjectUpgrader(
    IAnsiConsole console,
    DotNetProcess dotnet,
    DotNetUpgradeFinder upgradeFinder,
    IEnumerable<IUpgrader> upgraders,
    TimeProvider timeProvider,
    IOptions<UpgradeOptions> options,
    ILogger<ProjectUpgrader> logger)
{
    private const int EndOfLifeWarningDays = 100;

    /// <summary>
    /// Gets the version of the application.
    /// </summary>
    public static string Version { get; } =
        typeof(ProjectUpgrader)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion;

    private string ProjectPath => options.Value.ProjectPath;

    /// <summary>
    /// Upgrades the project.
    /// </summary>
    /// <param name="cancellationToken">The optional <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to upgrade the project.
    /// </returns>
    public virtual async Task<int> UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var upgrade = await upgradeFinder.GetUpgradeAsync(cancellationToken);

        if (upgrade is null)
        {
            console.WriteWarningLine("No eligible .NET upgrade was found.");
            console.WriteLine();
            return 0;
        }

        var name = Path.GetFileNameWithoutExtension(ProjectPath);

        console.MarkupLineInterpolated($"Upgrading project [aqua]{name}[/] to .NET [purple]{upgrade.Channel}[/]...");
        console.WriteLine();

        if (upgrade.EndOfLife is { } value)
        {
            var utcNow = timeProvider.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(utcNow);
            var warnFrom = value.AddDays(-EndOfLifeWarningDays);

            if (today >= warnFrom)
            {
                var eolUtc = value.ToDateTime(TimeOnly.MinValue);
                var days = (eolUtc - utcNow).TotalDays;

                console.WriteRuntimeNearingEndOfSupportWarning(upgrade, days);
                console.WriteLine();
            }
        }

        Log.Upgrading(logger, ProjectPath);

        UpgradeResult result = UpgradeResult.None;

        foreach (var upgrader in upgraders.OrderBy((p) => p.Priority))
        {
            UpgradeResult stepResult;

            try
            {
                stepResult = await upgrader.UpgradeAsync(upgrade, cancellationToken);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                throw;
            }
            catch (Exception ex)
            {
                stepResult = UpgradeResult.Error;

                console.WriteLine();
                console.WriteExceptionLine($"An error occurred while performing upgrade step {upgrader.GetType().Name}.", ex);
            }

            result = result.Max(stepResult);
        }

        if (result is UpgradeResult.Warning)
        {
            console.WriteLine();
            console.WriteWarningLine("One or more upgrade steps produced a warning.");
        }

        if (result is UpgradeResult.Success or UpgradeResult.Warning)
        {
            Log.Upgraded(
                logger,
                ProjectPath,
                upgrade.Channel.ToString(),
                upgrade.SdkVersion.ToString());

            if (options.Value.TestUpgrade)
            {
                console.WriteProgressLine("Verifying upgrade...");

                var projects = ProjectHelpers.FindProjects(ProjectPath);

                if (projects.Count is 0)
                {
                    result = result.Max(UpgradeResult.Warning);

                    console.WriteWarningLine("Could not find any test projects.");
                    console.WriteWarningLine("The project may not be in a working state.");
                }
                else
                {
                    var testResult = await console
                        .Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("green"))
                        .StartAsync(
                            $"[teal]Running tests...[/]",
                            async (context) => await RunTestsAsync(projects, context, cancellationToken));

                    result = result.Max(testResult.Success ? UpgradeResult.Success : UpgradeResult.Warning);

                    console.WriteLine();

                    if (testResult.Success)
                    {
                        console.WriteSuccessLine("Upgrade successfully tested.");
                    }
                    else
                    {
                        console.WriteWarningLine("The project upgrade did not result in a successful test run.");
                        console.WriteWarningLine("The project may not be in a working state.");

                        if (!string.IsNullOrWhiteSpace(testResult.StandardError))
                        {
                            console.WriteLine();
                            console.WriteProgressLine(testResult.StandardError);
                        }

                        if (!string.IsNullOrWhiteSpace(testResult.StandardOutput))
                        {
                            console.WriteLine();
                            console.WriteProgressLine(testResult.StandardOutput);
                        }
                    }
                }
            }
        }

        console.WriteLine();

        if (result is UpgradeResult.Success)
        {
            console.MarkupLine($"[aqua]{name}[/] upgrade to [white on purple].NET {upgrade.Channel}[/] [green]successful[/]! :rocket:");
        }
        else if (result is UpgradeResult.None)
        {
            Log.NothingToUpgrade(logger, ProjectPath);

            console.WriteWarningLine("The project upgrade did not result in any changes being made.");
            console.WriteWarningLine("Maybe the project has already been upgraded?");
        }
        else
        {
            (string emoji, string color, string description) = result switch
            {
                UpgradeResult.Warning => (":warning:", "yellow", "succeeded with warnings"),
                _ => (":cross_mark:", "red", "failed"),
            };

            console.MarkupLine($"[aqua]{name}[/] upgrade to [purple].NET {upgrade.Channel}[/] [{color}]{description}[/]! {emoji}");
        }

        const int Success = 0;
        const int Error = 1;

        return result switch
        {
            UpgradeResult.None or UpgradeResult.Success => Success,
            UpgradeResult.Warning => options.Value.TreatWarningsAsErrors ? Error : Success,
            _ => Error,
        };
    }

    private async Task<DotNetResult> RunTestsAsync(
        IReadOnlyList<string> projects,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        foreach (var project in projects)
        {
            string name = ProjectHelpers.RelativeName(ProjectPath, project);
            context.Status = $"[teal]Running tests for {name}...[/]";

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

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading project {ProjectPath}.")]
        public static partial void Upgrading(ILogger logger, string projectPath);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Successfully upgraded project {ProjectPath} to .NET {Channel} and .NET SDK {SdkVersion}.")]
        public static partial void Upgraded(
            ILogger logger,
            string projectPath,
            string channel,
            string sdkVersion);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Project {ProjectPath} did not contain any eligible changes to upgrade.")]
        public static partial void NothingToUpgrade(ILogger logger, string projectPath);
    }
}
