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

        if (!Directory.Exists(options.ProjectPath))
        {
            return ValidateOptionsResult.Fail($"The project path '{options.ProjectPath}' could not be found.");
        }

        return ValidateOptionsResult.Success;
    }
}
