// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing a log entry for a test from .NET Bumper. This class cannot be inherited.
/// </summary>
public sealed class BumperTestLogEntry
{
    /// <summary>
    /// Gets or sets the ID of the test.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the test.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("outcome")]
    public string? Outcome { get; set; }

    /// <summary>
    /// Gets or sets the error message associated with the test, if any.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("error")]
    public string? ErrorMessage { get; set; }
}
