// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper;

internal sealed class UpgradeOptionsValidator : IValidateOptions<UpgradeOptions>
{
    public ValidateOptionsResult Validate(string? name, UpgradeOptions options)
    {
        if (options.OpenPullRequest)
        {
            if (string.IsNullOrWhiteSpace(options.GitHubRepository))
            {
                return ValidateOptionsResult.Fail("The full name of the GitHub repository is required to open a pull request.");
            }

            if (string.IsNullOrWhiteSpace(options.GitHubToken))
            {
                return ValidateOptionsResult.Fail("A GitHub token is required to open a pull request.");
            }

            if (options.GitHubApiUri is null)
            {
                return ValidateOptionsResult.Fail("The URI of the GitHub API is required to open a pull request.");
            }
        }

        if (!Directory.Exists(options.ProjectPath))
        {
            return ValidateOptionsResult.Fail($"The project path '{options.ProjectPath}' could not be found.");
        }

        return ValidateOptionsResult.Success;
    }
}
