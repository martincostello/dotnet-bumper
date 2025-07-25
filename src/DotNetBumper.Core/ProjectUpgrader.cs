﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using MartinCostello.DotNetBumper.Logging;
using MartinCostello.DotNetBumper.PostProcessors;
using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that upgrades a project  to a newer version of .NET.
/// </summary>
/// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
/// <param name="environment">The current <see cref="IEnvironment"/>.</param>
/// <param name="upgradeFinder">The <see cref="DotNetUpgradeFinder"/> to use.</param>
/// <param name="upgraders">The <see cref="IUpgrader"/> implementations to use.</param>
/// <param name="postProcessors">The <see cref="IPostProcessor"/> implementations to use.</param>
/// <param name="logContext">The <see cref="BumperLogContext"/> to use.</param>
/// <param name="timeProvider">The <see cref="TimeProvider"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{ProjectUpgrader}"/> to use.</param>
public partial class ProjectUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    DotNetUpgradeFinder upgradeFinder,
    IEnumerable<IUpgrader> upgraders,
    IEnumerable<IPostProcessor> postProcessors,
    BumperLogContext logContext,
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
        logContext.StartedAt = timeProvider.GetUtcNow();

        var upgrade = await upgradeFinder.GetUpgradeAsync(cancellationToken);

        if (upgrade is null)
        {
            console.WriteWarningLine("No eligible .NET upgrade was found.");
            console.WriteLine();
            return 0;
        }

        logContext.DotNetVersion = upgrade.Channel.ToString();

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

                console.WriteRuntimeNearingEndOfSupportWarning(environment, upgrade, days);
                console.WriteLine();
            }
        }

        Log.Upgrading(logger, ProjectPath);

        var result = ProcessingResult.None;

        foreach (var upgrader in Order(upgraders))
        {
            ProcessingResult stepResult;

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
                stepResult = ProcessingResult.Error;

                console.WriteLine();
                console.WriteExceptionLine($"An error occurred while performing upgrade step {upgrader.GetType().Name}.", ex);
            }

            result = result.Max(stepResult);
        }

        if (result is ProcessingResult.Warning)
        {
            console.WriteLine();
            console.WriteWarningLine("One or more upgrade steps produced a warning.");
            console.WriteLine();
        }

        if (result is ProcessingResult.Success or ProcessingResult.Warning)
        {
            Log.Upgraded(
                logger,
                ProjectPath,
                upgrade.Channel,
                upgrade.SdkVersion);

            foreach (var processor in Order(postProcessors))
            {
                ProcessingResult stepResult;

                try
                {
                    stepResult = await processor.PostProcessAsync(upgrade, cancellationToken);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stepResult = ProcessingResult.Error;

                    console.WriteLine();
                    console.WriteExceptionLine($"An error occurred while performing post-processing step {processor.GetType().Name}.", ex);
                }

                result = result.Max(stepResult);
            }
        }

        logContext.Result = result.ToString();

        console.WriteLine();

        if (result is ProcessingResult.Success)
        {
            console.MarkupLine($"[aqua]{name}[/] upgrade to [white on purple].NET {upgrade.Channel}[/] [green]successful[/]! :rocket:");
            console.WriteLine();
            console.WriteDisclaimer(environment, upgrade.Channel);
        }
        else if (result is ProcessingResult.None)
        {
            Log.NothingToUpgrade(logger, ProjectPath);

            console.WriteWarningLine("The project upgrade did not result in any changes being made.");
            console.WriteWarningLine("Maybe the project has already been upgraded?");
        }
        else
        {
            (string emoji, string color, string description) = result switch
            {
                ProcessingResult.Warning => (Emoji.Known.Warning, "yellow", "completed with warnings"),
                _ => (Emoji.Known.CrossMark, "red", "failed"),
            };

            console.MarkupLine($"[aqua]{name}[/] upgrade to [purple].NET {upgrade.Channel}[/] [{color}]{description}[/]! {emoji}");
        }

        await WriteLogsAsync(cancellationToken);

        const int Success = 0;
        const int Error = 1;

        return result switch
        {
            ProcessingResult.None or ProcessingResult.Success => Success,
            ProcessingResult.Warning => options.Value.TreatWarningsAsErrors ? Error : Success,
            _ => Error,
        };
    }

    private static IEnumerable<IUpgrader> Order(IEnumerable<IUpgrader> upgraders)
    {
        var sequence = new List<IUpgrader>(upgraders);

        // The SDK version in global.json must be updated first
        var index = sequence.FindIndex((p) => p is Upgraders.GlobalJsonUpgrader);

        if (index is > -1)
        {
            var globalJson = sequence[index];
            sequence.RemoveAt(index);
            sequence.Insert(0, globalJson);
        }

        // The NuGet configuration needs to be updated before any package updates
        index = sequence.FindIndex((p) => p is Upgraders.NuGetConfigUpgrader);

        if (index is > -1)
        {
            var nugetConfig = sequence[index];
            sequence.RemoveAt(index);
            sequence.Insert(1, nugetConfig);
        }

        // Packages need to be updated after the TFM so the packages relate to the update but before any code is changed
        index = sequence.FindIndex((p) => p is Upgraders.PackageVersionUpgrader);

        if (index is > -1)
        {
            var packageVersions = sequence[index];
            sequence.RemoveAt(index);
            sequence.Add(packageVersions);
        }

        // The code upgrader/formatter must run after all other upgraders as analyzers may
        // have come as part of NuGet packages that were upgraded by dotnet-outdated-tool.
        index = sequence.FindIndex((p) => p is Upgraders.DotNetCodeUpgrader);

        if (index is > -1)
        {
            var code = sequence[index];
            sequence.RemoveAt(index);
            sequence.Add(code);
        }

        foreach (var item in sequence)
        {
            yield return item;
        }
    }

    private static IEnumerable<IPostProcessor> Order(IEnumerable<IPostProcessor> upgraders)
    {
        // No special ordering required yet
        foreach (var item in upgraders)
        {
            yield return item;
        }
    }

    private async Task WriteLogsAsync(CancellationToken cancellationToken)
    {
        IBumperLogWriter writer = options.Value.LogFormat switch
        {
            BumperLogFormat.GitHubActions => new GitHubActionsLogWriter(),
            BumperLogFormat.Json => new JsonLogWriter(options.Value.LogPath ?? "dotnet-bumper.json"),
            BumperLogFormat.Markdown => new MarkdownLogWriter(options.Value.LogPath ?? "dotnet-bumper.md"),
            _ => new NullLogWriter(),
        };

        logContext.FinishedAt = timeProvider.GetUtcNow();

        await writer.WriteAsync(logContext, cancellationToken);
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
            Version channel,
            NuGetVersion sdkVersion);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Project {ProjectPath} did not contain any eligible changes to upgrade.")]
        public static partial void NothingToUpgrade(ILogger logger, string projectPath);
    }
}
