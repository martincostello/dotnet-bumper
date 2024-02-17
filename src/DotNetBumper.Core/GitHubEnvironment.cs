// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// See <c>https://docs.github.com/actions/learn-github-actions/variables</c>.
/// </summary>
internal static class GitHubEnvironment
{
    internal const string ApiUrl = "GITHUB_API_URL";
    internal const string Repository = "GITHUB_REPOSITORY";
    internal const string Token = "GITHUB_TOKEN";
}
