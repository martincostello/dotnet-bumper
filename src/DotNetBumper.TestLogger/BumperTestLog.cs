// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing the .NET Bumper log output for VSTest.
/// </summary>
public sealed class BumperTestLog
{
    /// <summary>
    /// Gets or sets the test outcomes for each test container.
    /// </summary>
    [JsonPropertyName("outcomes")]
    public IDictionary<string, IList<BumperTestLogEntry>> Outcomes { get; set; } = new Dictionary<string, IList<BumperTestLogEntry>>();

    /// <summary>
    /// Gets or sets the test outcome summary for each test container.
    /// </summary>
    [JsonPropertyName("summary")]
    public IDictionary<string, IDictionary<string, long>> Summary { get; set; } = new Dictionary<string, IDictionary<string, long>>();
}
