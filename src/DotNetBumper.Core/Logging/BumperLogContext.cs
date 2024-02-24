// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

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
}
