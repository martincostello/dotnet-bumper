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
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{ProjectUpgrader}"/> to use.</param>
public partial class ProjectUpgrader(
    IAnsiConsole console,
    DotNetUpgradeFinder upgradeFinder,
    IEnumerable<IUpgrader> upgraders,
    IOptions<UpgradeOptions> options,
    ILogger<ProjectUpgrader> logger)
{
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
            console.WriteLine("No eligible .NET upgrade was found.");
            return;
        }

        console.WriteLine($"Upgrading project to .NET {upgrade.Channel}...");

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

            console.WriteLine($"Project upgraded to .NET {upgrade.Channel}.");

            if (options.Value.OpenPullRequest)
            {
                // TODO Open pull request
            }
        }
        else
        {
            Log.NothingToUpgrade(logger, options.Value.ProjectPath);
            console.WriteLine("The project upgrade did not result in any changes being made.");
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
