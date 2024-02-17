// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class GlobalJsonUpgrader(
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetSdk(logger);

        bool filesChanged = false;

        var files = Directory.GetFiles(options.Value.ProjectPath, "global.json", SearchOption.AllDirectories);

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

            if (sdkVersion < upgrade.SdkVersion)
            {
                string upgradeVersion = upgrade.SdkVersion.ToString();

                json = json.Replace($@"""{versionString}""", $@"""{upgradeVersion}""", StringComparison.Ordinal);

                await File.WriteAllTextAsync(path, json, cancellationToken);

                Log.UpgradedDotNetSdk(logger, path, sdkVersion.ToString(), upgradeVersion);

                filesChanged = true;
            }
        }

        return filesChanged;
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
