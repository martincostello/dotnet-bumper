// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper;

internal sealed class UpgradeOptionsValidator : IValidateOptions<UpgradeOptions>
{
    public ValidateOptionsResult Validate(string? name, UpgradeOptions options)
    {
        if (options.DotNetChannel is { Length: > 0 } version &&
            !Version.TryParse(version, out _))
        {
            return ValidateOptionsResult.Fail($"The specified .NET channel \"{version}\" is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectPath))
        {
            return ValidateOptionsResult.Fail("No path to a directory containing a .NET project/solution to upgrade specified.");
        }

        if (!Directory.Exists(options.ProjectPath))
        {
            return ValidateOptionsResult.Fail($"The project path '{options.ProjectPath}' could not be found.");
        }

        if (options.LogPath is { Length: > 0 } &&
            options.LogFormat is BumperLogFormat.None or BumperLogFormat.GitHubActions)
        {
            return ValidateOptionsResult.Fail($"The log path option is not valid for use with the \"{options.LogFormat}\" log format.");
        }

        return ValidateOptionsResult.Success;
    }
}
