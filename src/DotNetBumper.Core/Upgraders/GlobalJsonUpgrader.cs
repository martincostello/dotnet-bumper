// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class GlobalJsonUpgrader(
    HttpClient httpClient,
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetSdk(logger);

        string projectPath = options.Value.ProjectPath;

        var channels = await GetChannelsAsync(cancellationToken);

        bool filesChanged = false;

        var files = Directory.GetFiles(projectPath, "global.json", SearchOption.AllDirectories);

        foreach (var path in files)
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);
            using var globalJson = JsonDocument.Parse(json);

            if (!globalJson.RootElement.TryGetProperty("sdk", out var sdk))
            {
                // TODO Log
                continue;
            }

            if (!sdk.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.String)
            {
                // TODO Log
                continue;
            }

            var versionString = version.GetString();

            if (!NuGetVersion.TryParse(versionString, out var sdkVersion))
            {
                // TODO Log
                continue;
            }

            var upgradeVersion = GetUpgradeVersion(sdkVersion, channels);

            if (upgradeVersion is { })
            {
                json = json.Replace($@"""{versionString}""", $@"""{upgradeVersion}""", StringComparison.Ordinal);

                await File.WriteAllTextAsync(path, json, cancellationToken);

                Log.UpgradedDotNetSdk(logger, path, sdkVersion.ToString(), upgradeVersion);

                filesChanged = true;
            }
        }

        return filesChanged;
    }

    private async Task<IReadOnlyList<DotNetChannel>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        using var releasesIndex = await httpClient.GetFromJsonAsync<JsonDocument>(
            "https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json",
            cancellationToken);

        if (releasesIndex is null)
        {
            return [];
        }

        var releases = releasesIndex.RootElement.GetProperty("releases-index");

        var channels = new List<DotNetChannel>();

        foreach (var release in releases.EnumerateArray())
        {
            var channel = release.GetProperty("channel-version").GetString();
            var latestSdk = release.GetProperty("latest-sdk").GetString();
            var releaseType = release.GetProperty("release-type").GetString();

            if (channel is null || latestSdk is null)
            {
                // TODO Log
                continue;
            }

            channels.Add(new()
            {
                Channel = Version.Parse(channel),
                IsLts = releaseType is "lts",
                LatestSdkVersion = NuGetVersion.Parse(latestSdk),
            });
        }

        return channels;
    }

    private string? GetUpgradeVersion(
        NuGetVersion sdkVersion,
        IReadOnlyList<DotNetChannel> channels)
    {
        var currentChannel = new Version(sdkVersion.Major, sdkVersion.Minor);
        var desiredChannel = options.Value.DotNetChannel is { } desired ? Version.Parse(desired) : null;

        bool IsEligble(DotNetChannel channel)
        {
            if (desiredChannel is not null)
            {
                return channel.Channel == desiredChannel;
            }

            return options.Value.ReleaseType switch
            {
                DotNetReleaseType.Preview => channel.LatestSdkVersion.IsPrerelease,
                DotNetReleaseType.Lts => channel.IsLts && !channel.LatestSdkVersion.IsPrerelease,
                _ => !channel.LatestSdkVersion.IsPrerelease,
            };
        }

        var latestChannel = channels
            .Where((p) => p.Channel >= currentChannel)
            .Where((p) => p.LatestSdkVersion > sdkVersion)
            .Where(IsEligble)
            .OrderByDescending((p) => p.Channel)
            .FirstOrDefault();

        return latestChannel?.LatestSdkVersion.ToString();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Upgrading .NET SDK version.")]
        public static partial void UpgradingDotNetSdk(ILogger logger);

        [LoggerMessage(
           EventId = 2,
           Level = LogLevel.Debug,
           Message = "Upgrading .NET SDK version in {FileName} from {PreviousVersion} to {UpgradedVersion}.")]
        public static partial void UpgradedDotNetSdk(
            ILogger logger,
            string fileName,
            string previousVersion,
            string upgradedVersion);
    }

    private sealed class DotNetChannel
    {
        public required Version Channel { get; set; }

        public required NuGetVersion LatestSdkVersion { get; set; }

        public required bool IsLts { get; set; }
    }
}
