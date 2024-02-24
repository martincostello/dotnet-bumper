// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal static class EnvironmentHelpers
{
    public static bool IsGitHubActions { get; } = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is "true";
}
