// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing the custom configuration for .NET Bumper. This class cannot be inherited.
/// </summary>
internal sealed class BumperConfiguration
{
    /// <summary>
    /// Gets or sets the NuGet packages to exclude from updating.
    /// </summary>
    [JsonPropertyName("excludeNuGetPackages")]
    public HashSet<string> ExcludeNuGetPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets the NuGet packages to include when updating.
    /// </summary>
    [JsonPropertyName("includeNuGetPackages")]
    public HashSet<string> IncludeNuGetPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets any MSBuild warnings to ignore.
    /// </summary>
    [JsonPropertyName("noWarn")]
    public HashSet<string> NoWarn { get; set; } = [];

    /// <summary>
    /// Gets or sets the project-path(s) to ignore from searching for remaining references.
    /// </summary>
    [JsonPropertyName("remainingReferencesIgnore")]
    public HashSet<string> RemainingReferencesIgnore { get; set; } = [];
}
