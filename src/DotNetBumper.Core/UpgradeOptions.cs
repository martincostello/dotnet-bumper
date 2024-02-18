// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing the options for upgrading a project.
/// </summary>
public sealed class UpgradeOptions
{
    /// <summary>
    /// Gets or sets a specific .NET release channel to upgrade to.
    /// </summary>
    public string? DotNetChannel { get; set; }

    /// <summary>
    /// Gets or sets the path of the project to upgrade.
    /// </summary>
    [Required]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value the type of upgrade to perform.
    /// </summary>
    public UpgradeType UpgradeType { get; set; }
}
