// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace MartinCostello.DotNetBumper;

internal sealed partial class UpgradePostConfigureOptions(
    IConfiguration configuration,
    ILogger<UpgradePostConfigureOptions> logger) : IPostConfigureOptions<UpgradeOptions>
{
    public void PostConfigure(string? name, UpgradeOptions options)
    {
        options.GitHubRepository ??= configuration[GitHubEnvironment.Repository];
        options.GitHubToken ??= configuration[GitHubEnvironment.Token];

        if (options.GitHubApiUri is null)
        {
            if (configuration[GitHubEnvironment.ApiUrl] is { } apiUrl)
            {
                if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri))
                {
                    Log.InvalidGitHubApiUrl(logger, apiUrl, GitHubEnvironment.ApiUrl);
                }

                options.GitHubApiUri = apiUri;
            }

            options.GitHubApiUri ??= GitHubClient.GitHubApiUrl;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Warning,
           Message = "The configured value of the {VariableName} environment variable, {Url}, is not a valid URI.")]
        public static partial void InvalidGitHubApiUrl(ILogger logger, string url, string variableName);
    }
}
