// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using MartinCostello.DotNetBumper.Upgrades;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that upgrades a project  to a newer version of .NET.
/// </summary>
/// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
/// <param name="upgradeFinder">The <see cref="DotNetUpgradeFinder"/> to use.</param>
/// <param name="upgraders">The <see cref="IUpgrader"/> implementations to use.</param>
/// <param name="timeProvider">The <see cref="TimeProvider"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{ProjectUpgrader}"/> to use.</param>
public partial class ProjectUpgrader(
    IAnsiConsole console,
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

    /// <summary>
    /// Upgrades the project.
    /// </summary>
    /// <param name="cancellationToken">The optional <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to upgrade the project.
    /// </returns>
    public virtual async Task UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var upgrade = await upgradeFinder.GetUpgradeAsync(cancellationToken);

        if (upgrade is null)
        {
            console.MarkupLine("[yellow]:warning: No eligible .NET upgrade was found.[/]");
            console.WriteLine();
            return;
        }

        var name = Path.GetFileNameWithoutExtension(options.Value.ProjectPath);

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

                console.MarkupLineInterpolated($"[yellow]:warning: Support for .NET {upgrade.Channel} ends in {days:N0} days on {eolUtc:D}.[/]");
                console.MarkupLine("[yellow]:warning: See https://dotnet.microsoft.com/platform/support/policy/dotnet-core for more information.[/]");
                console.WriteLine();
            }
        }

        Log.Upgrading(logger, options.Value.ProjectPath);

        bool hasChanges = false;

        foreach (var upgrader in upgraders)
        {
            hasChanges |= await upgrader.UpgradeAsync(upgrade, cancellationToken);
        }

        if (hasChanges)
        {
            Log.Upgraded(
                logger,
                options.Value.ProjectPath,
                upgrade.Channel.ToString(),
                upgrade.SdkVersion.ToString());

            console.WriteLine();
            console.MarkupLine($"[aqua]{name}[/] upgraded to [white on purple].NET {upgrade.Channel}[/] :rocket:!");
        }
        else
        {
            Log.NothingToUpgrade(logger, options.Value.ProjectPath);

            console.WriteLine();
            console.MarkupLine("[yellow]:warning: The project upgrade did not result in any changes being made.[/]");
            console.MarkupLine("[yellow]:warning: Maybe the project has already been upgraded?[/]");
        }
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
