// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that upgrades a project  to a newer version of .NET.
/// </summary>
/// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{ProjectUpgrader}"/> to use.</param>
public partial class ProjectUpgrader(
    IAnsiConsole console,
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
        console.WriteLine("Upgrading project...");

        Log.UpgradingProject(logger, options.Value.ProjectPath);

        await Task.CompletedTask;

        Log.UpgradedProject(logger, options.Value.ProjectPath);

        console.WriteLine("Project upgraded.");
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Upgrading project {ProjectPath}.")]
        public static partial void UpgradingProject(ILogger logger, string projectPath);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Upgrading project {ProjectPath} successfully.")]
        public static partial void UpgradedProject(ILogger logger, string projectPath);
    }
}
