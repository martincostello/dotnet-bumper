// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.PostProcessors;

namespace MartinCostello.DotNetBumper.Logging;

/// <summary>
/// A class representing the logging context for .NET Bumper. This class cannot be inherited.
/// </summary>
public sealed class BumperLogContext
{
    /// <summary>
    /// Gets or sets the date and time the upgrade started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time the upgrade finished.
    /// </summary>
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>
    /// Gets or sets the version of the .NET SDK that was found to upgrade to, if any.
    /// </summary>
    public string? DotNetSdkVersion { get; set; }

    /// <summary>
    /// Gets or sets the build logs, if any.
    /// </summary>
    public BumperBuildLog? BuildLogs { get; set; }

    /// <summary>
    /// Gets or sets the test logs, if any.
    /// </summary>
    public BumperTestLog? TestLogs { get; set; }

    /// <summary>
    /// Gets or sets any warnings encountered during the upgrade.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the potential file edits, if any.
    /// </summary>
    internal IDictionary<ProjectFile, List<PotentialFileEdit>> PotentialEdits { get; set; } = new Dictionary<ProjectFile, List<PotentialFileEdit>>();
}
