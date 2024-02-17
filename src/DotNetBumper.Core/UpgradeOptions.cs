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
    /// Gets or sets the URI of the GitHub API to use.
    /// </summary>
    public Uri? GitHubApiUri { get; set; }

    /// <summary>
    /// Gets or sets the full name of the GitHub repository for the project.
    /// </summary>
    public string? GitHubRepository { get; set; }

    /// <summary>
    /// Gets or sets the GitHub token to use.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to open a pull request after upgrading the project.
    /// </summary>
    public bool OpenPullRequest { get; set; }

    /// <summary>
    /// Gets or sets the path of the project to upgrade.
    /// </summary>
    [Required]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value the release type to upgrade to.
    /// </summary>
    public DotNetReleaseType ReleaseType { get; set; }
}
