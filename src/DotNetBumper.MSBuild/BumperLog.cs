// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing the .NET Bumper log output for MSBuild.
/// </summary>
public sealed class BumperLog
{
    /// <summary>
    /// Gets or sets the log entries.
    /// </summary>
    [JsonPropertyName("entries")]
    public IList<BumperLogEntry> Entries { get; set; } = [];
}
