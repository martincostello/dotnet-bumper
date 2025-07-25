﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
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
/// <param name="timeProvider">The <see cref="TimeProvider"/> to use.</param>
/// <param name="options">The <see cref="IOptions{UpgradeOptions}"/> to use.</param>
/// <param name="logger">The <see cref="ILogger{DotNetUpgradeFinder}"/> to use.</param>
public partial class DotNetUpgradeFinder(
    HttpClient httpClient,
    TimeProvider timeProvider,
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
        if (options.Value.UpgradeType is UpgradeType.Daily)
        {
            return await GetDailyBuildAsync(cancellationToken);
        }

        var candidates = await GetChannelsAsync(cancellationToken);
        return GetUpgrade(candidates);
    }

    private static DateOnly GetUpdateTuesday(DateOnly monthAndYear)
    {
        var result = new DateOnly(monthAndYear.Year, monthAndYear.Month, 1);
        var count = 0;

        while (count < 2)
        {
            if (result.DayOfWeek == DayOfWeek.Tuesday)
            {
                count++;
            }

            if (count is 2)
            {
                break;
            }

            result = result.AddDays(1);
        }

        Debug.Assert(result.DayOfWeek is DayOfWeek.Tuesday, "Failed to determine Update Tuesday date.");

        return result;
    }

    private static DotNetReleaseType GetDotNetReleaseType(NuGetVersion version)
        => version.Major % 2 is 0 ? DotNetReleaseType.Lts : DotNetReleaseType.Sts;

    private Version GetDotNetDevelopmentVersion()
    {
        var releaseDate = GetDotNetReleaseDate();
        var major = releaseDate.Year - 2015;

        return new(major, 0);
    }

    private DateOnly GetDotNetReleaseDate()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var releaseDate = GetUpdateTuesday(new(today.Year, 11, 1));

        if (today > releaseDate)
        {
            releaseDate = GetUpdateTuesday(new(today.Year + 1, 11, 1));
        }

        return releaseDate;
    }

    private async Task<UpgradeInfo?> GetDailyBuildAsync(CancellationToken cancellationToken)
    {
#pragma warning disable CA1308
        var quality = options.Value.Quality.ToString().ToLowerInvariant();
        var channel = options.Value.DotNetChannel is null ? GetDotNetDevelopmentVersion() : Version.Parse(options.Value.DotNetChannel);
#pragma warning restore CA1307

        var versionUrl = $"https://aka.ms/dotnet/{channel.ToString(2)}/{quality}/sdk-productVersion.txt";

        using var response = await httpClient.GetAsync(versionUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.UnableToDetermineLatestSdkBuildHttpError(logger, response.StatusCode);
            return null;
        }

        if (response.Content.Headers.ContentType?.MediaType is not ("application/octet-stream" or "text/plain"))
        {
            Log.UnableToDetermineLatestSdkBuildContentType(logger, response.Content.Headers.ContentType?.MediaType);
            return null;
        }

        var version = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!NuGetVersion.TryParse(version.Trim(), out var sdkVersion))
        {
            Log.UnableToParseLatestSdkBuildVersion(logger, version);
            return null;
        }

        var installer = new DotNetInstaller(httpClient, logger);
        await installer.TryInstallAsync(sdkVersion, cancellationToken);

        return new()
        {
            Channel = channel,
            EndOfLife = GetDotNetReleaseDate(),
            ReleaseType = GetDotNetReleaseType(sdkVersion),
            SdkVersion = sdkVersion,
            SupportPhase = DotNetSupportPhase.Preview,
        };
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
                UpgradeType.Daily => true,
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
            latestChannel.Channel,
            latestChannel.SdkVersion,
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
            Version channel,
            NuGetVersion sdkVersion,
            DotNetReleaseType releaseType);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Error,
            Message = "Failed to determine the latest daily build version for the .NET SDK: {StatusCode}")]
        public static partial void UnableToDetermineLatestSdkBuildHttpError(ILogger logger, HttpStatusCode statusCode);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Error,
            Message = "Failed to determine the latest daily build version for the .NET SDK due to unexpected content type {ContentType}.")]
        public static partial void UnableToDetermineLatestSdkBuildContentType(ILogger logger, string? contentType);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Error,
            Message = "Failed to parse the latest daily build version for the .NET SDK: {Content}")]
        public static partial void UnableToParseLatestSdkBuildVersion(ILogger logger, string? content);
    }

    [ExcludeFromCodeCoverage]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSourceGenerationOptions]
    private sealed partial class DotNetJsonSerializationContext : JsonSerializerContext;
}
