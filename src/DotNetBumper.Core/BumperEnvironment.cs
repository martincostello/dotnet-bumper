// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Spectre.Console;

namespace MartinCostello.DotNetBumper;

internal sealed class BumperEnvironment(IAnsiConsole console) : IEnvironment
{
    public bool IsGitHubActions => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is "true";

    public bool SupportsLinks => console.Profile.Capabilities.Links && !IsGitHubActions;
}
