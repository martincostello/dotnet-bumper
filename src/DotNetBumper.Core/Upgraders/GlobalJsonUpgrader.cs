// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class GlobalJsonUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetSdk(logger);

        console.WriteLine("Upgrading .NET SDK...");

        bool filesChanged = false;

        var files = Directory.GetFiles(
            options.Value.ProjectPath,
            "global.json",
            SearchOption.AllDirectories);

        foreach (var path in files)
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken);

            if (!TryParseSdkVersion(json, out var currentVersion))
            {
                Log.ParseSdkVersionFailed(logger, path);
                continue;
            }

            if (currentVersion < upgrade.SdkVersion)
            {
                json = json.Replace($@"""{currentVersion}""", $@"""{upgrade.SdkVersion}""", StringComparison.Ordinal);

                await File.WriteAllTextAsync(path, json, cancellationToken);

                Log.UpgradedDotNetSdk(
                    logger,
                    path,
                    currentVersion.ToString(),
                    upgrade.SdkVersion.ToString());

                filesChanged = true;
            }
        }

        return filesChanged;
    }

    private static bool TryParseSdkVersion(
        string json,
        [NotNullWhen(true)] out NuGetVersion? sdkVersion)
    {
        sdkVersion = null;

        using var globalJson = JsonDocument.Parse(json);

        if (globalJson.RootElement.TryGetProperty("sdk", out var sdk) &&
            sdk.TryGetProperty("version", out var version) &&
            version.ValueKind == JsonValueKind.String &&
            NuGetVersion.TryParse(version.GetString(), out sdkVersion))
        {
            return true;
        }

        return false;
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
           Level = LogLevel.Warning,
           Message = "Unable to parse .NET SDK version from {FileName}.")]
        public static partial void ParseSdkVersionFailed(ILogger logger, string fileName);

        [LoggerMessage(
           EventId = 3,
           Level = LogLevel.Debug,
           Message = "Upgraded .NET SDK version in {FileName} from {PreviousVersion} to {UpgradedVersion}.")]
        public static partial void UpgradedDotNetSdk(
            ILogger logger,
            string fileName,
            string previousVersion,
            string upgradedVersion);
    }
}
