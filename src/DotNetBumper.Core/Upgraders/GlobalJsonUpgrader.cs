// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class GlobalJsonUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : FileUpgrader(console, options, logger)
{
    public override int Priority => -1;

    protected override string Action => "Upgrading .NET SDK";

    protected override string InitialStatus => "Update SDK version";

    protected override IReadOnlyList<string> Patterns => ["global.json"];

    protected override async Task<UpgradeResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetSdk(logger);

        UpgradeResult result = UpgradeResult.None;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            string json = await File.ReadAllTextAsync(path, cancellationToken);

            if (!TryParseSdkVersion(json, out var currentVersion))
            {
                Log.ParseSdkVersionFailed(logger, path);

                result = result.Max(UpgradeResult.Warning);
                continue;
            }

            if (currentVersion < upgrade.SdkVersion)
            {
                context.Status = StatusMessage($"Updating {name}...");

                json = json.Replace($@"""{currentVersion}""", $@"""{upgrade.SdkVersion}""", StringComparison.Ordinal);

                await File.WriteAllTextAsync(path, json, cancellationToken);

                Log.UpgradedDotNetSdk(
                    logger,
                    path,
                    currentVersion.ToString(),
                    upgrade.SdkVersion.ToString());

                result = result.Max(UpgradeResult.Success);
            }
        }

        return result;
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
