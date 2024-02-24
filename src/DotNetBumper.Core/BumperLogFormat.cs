// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing the available log output formats.
/// </summary>
public enum BumperLogFormat
{
    /// <summary>
    /// No log is generated.
    /// </summary>
    None = 0,

    /// <summary>
    /// The log is generated in JSON format.
    /// </summary>
    Json,

    /// <summary>
    /// The log is generated in Markdown format.
    /// </summary>
    Markdown,

    /// <summary>
    /// The log is written to the GitHub Actions workflow run step summary.
    /// </summary>
    GitHubActions,
}
