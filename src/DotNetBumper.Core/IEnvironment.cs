// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// Represents the environment in which the application is running.
/// </summary>
public interface IEnvironment
{
    /// <summary>
    /// Gets a value indicating whether the application is running in GitHub Actions.
    /// </summary>
    bool IsGitHubActions => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is "true";
}
