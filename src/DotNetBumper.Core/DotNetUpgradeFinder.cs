// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper.Upgraders;

/// <summary>
/// A class that finds the latest version of .NET available to upgrade to.
/// </summary>
/// <param name="httpClient">The <see cref="HttpClient"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{DotNetUpgradeFinder}"/> to use.</param>
public partial class DotNetUpgradeFinder(
    HttpClient httpClient,
    IOptions<UpgradeOptions> options,
    ILogger<DotNetUpgradeFinder> logger)
{
    /// <summary>
    /// Gets the latest eligible version of .NET available to upgrade to, if any.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation to
    /// get the latest version of .NET available to upgrade to, if any.
    /// </returns>
    public virtual async Task<UpgradeInfo?> GetUpgradeAsync(CancellationToken cancellationToken)
    {
        var candidates = await GetChannelsAsync(cancellationToken);
        return GetUpgrade(candidates);
    }

    private async Task<IReadOnlyList<UpgradeInfo>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        Log.GetReleaseNotes(logger);

        using var releasesIndex = await httpClient.GetFromJsonAsync<JsonDocument>(
            "https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json",
            DotNetJsonSerializationContext.Default.JsonDocument,
            cancellationToken);

        if (releasesIndex is null)
        {
            Log.NoDotNetReleasesFound(logger);
            return [];
        }

        var releases = releasesIndex.RootElement.GetProperty("releases-index");
        var channels = new List<UpgradeInfo>();

        foreach (var release in releases.EnumerateArray())
        {
            if (!TryParseRelease(
                    release,
                    out var channel,
                    out var sdkVersion,
                    out var releaseType,
                    out var supportPhase,
                    out var endOfLifeDate))
            {
                Log.UnableToParseRelease(logger, release);
                continue;
            }
            else if (supportPhase is DotNetSupportPhase.Eol)
            {
                continue;
            }

            channels.Add(new()
            {
                Channel = channel,
                EndOfLife = endOfLifeDate,
                ReleaseType = releaseType,
                SdkVersion = sdkVersion,
                SupportPhase = supportPhase,
            });
        }

        return channels;

        static bool TryParseRelease(
            JsonElement element,
            [NotNullWhen(true)] out Version? channel,
            [NotNullWhen(true)] out NuGetVersion? sdkVersion,
            out DotNetReleaseType releaseType,
            out DotNetSupportPhase supportPhase,
            out DateOnly? endOfLife)
        {
            channel = null;
            sdkVersion = null;
            releaseType = default;
            supportPhase = default;
            endOfLife = default;

            var channelString = GetString(element, "channel-version");
            var latestSdkVersion = GetString(element, "latest-sdk");
            var releaseTypeString = GetString(element, "release-type");
            var supportPhaseString = GetString(element, "support-phase");
            var endOfLifeString = GetString(element, "eol-date");

            if (endOfLifeString is { Length: > 0 })
            {
                if (DateOnly.TryParseExact(
                        endOfLifeString,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateOnly eol))
                {
                    endOfLife = eol;
                }
            }

            return
                Version.TryParse(channelString, out channel) &&
                NuGetVersion.TryParse(latestSdkVersion, out sdkVersion) &&
                Enum.TryParse(releaseTypeString, ignoreCase: true, out releaseType) &&
                Enum.TryParse(supportPhaseString.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true, out supportPhase);

            static string GetString(JsonElement element, string name)
            {
                if (element.TryGetProperty(name, out var property) &&
                    property.ValueKind is JsonValueKind.String &&
                    property.GetString() is { Length: > 0 } value)
                {
                    return value;
                }

                return string.Empty;
            }
        }
    }

    private UpgradeInfo? GetUpgrade(IReadOnlyList<UpgradeInfo> channels)
    {
        var desiredChannel = options.Value.DotNetChannel is { } desired ? Version.Parse(desired) : null;

        bool IsEligble(UpgradeInfo channel)
        {
            if (desiredChannel is not null)
            {
                return channel.Channel == desiredChannel;
            }

            return options.Value.UpgradeType switch
            {
                UpgradeType.Preview => channel.SdkVersion.IsPrerelease,
                UpgradeType.Lts => channel.ReleaseType is DotNetReleaseType.Lts && !channel.SdkVersion.IsPrerelease,
                _ => !channel.SdkVersion.IsPrerelease,
            };
        }

        var latestChannel = channels
            .Where(IsEligble)
            .OrderByDescending((p) => p.Channel)
            .FirstOrDefault();

        if (latestChannel is null)
        {
            return null;
        }

        Log.FoundEligibleUpgrade(
            logger,
            latestChannel.Channel.ToString(),
            latestChannel.SdkVersion.ToFullString(),
            latestChannel.ReleaseType);

        return latestChannel;
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Getting .NET release notes from GitHub.")]
        public static partial void GetReleaseNotes(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "No .NET releases found in GitHub.")]
        public static partial void NoDotNetReleasesFound(ILogger logger);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Unable to parse .NET release JSON: {Channel}.")]
        public static partial void UnableToParseRelease(ILogger logger, JsonElement channel);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Information,
            Message = "Found eligible .NET upgrade. Channel: {Channel}, SDK Version: {SdkVersion}, Release Type: {ReleaseType}.")]
        public static partial void FoundEligibleUpgrade(
            ILogger logger,
            string channel,
            string sdkVersion,
            DotNetReleaseType releaseType);
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSourceGenerationOptions]
    private sealed partial class DotNetJsonSerializationContext : JsonSerializerContext;
}
