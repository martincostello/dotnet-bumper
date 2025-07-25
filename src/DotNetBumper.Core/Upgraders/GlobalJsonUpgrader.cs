﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class GlobalJsonUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading .NET SDK";

    protected override string InitialStatus => "Update SDK version";

    protected override IReadOnlyList<string> Patterns { get; } = [WellKnownFileNames.GlobalJson];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetSdk(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            string json = await File.ReadAllTextAsync(path, cancellationToken);

            if (!TryParseSdkVersion(json, out var currentVersion))
            {
                Log.ParseSdkVersionFailed(logger, path);

                result = result.Max(ProcessingResult.Warning);
                continue;
            }

            if (currentVersion < upgrade.SdkVersion)
            {
                context.Status = StatusMessage($"Updating {name}...");

                json = json.Replace($@"""{currentVersion}""", $@"""{upgrade.SdkVersion}""", StringComparison.Ordinal);

                FileMetadata metadata = FileHelpers.GetMetadata(path);

                await File.WriteAllTextAsync(path, json, metadata.Encoding, cancellationToken);

                Log.UpgradedDotNetSdk(
                    logger,
                    path,
                    currentVersion,
                    upgrade.SdkVersion);

                logContext.DotNetSdkVersion = upgrade.SdkVersion.ToString();

                result = result.Max(ProcessingResult.Success);
            }
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update .NET SDK to `{upgrade.SdkVersion}`");
        }

        return result;
    }

    private static bool TryParseSdkVersion(
        string json,
        [NotNullWhen(true)] out NuGetVersion? sdkVersion)
    {
        sdkVersion = null;

        try
        {
            using var globalJson = JsonDocument.Parse(json, JsonHelpers.DocumentOptions);

            if (globalJson.RootElement.ValueKind == JsonValueKind.Object &&
                globalJson.RootElement.TryGetProperty("sdk", out var sdk) &&
                sdk.ValueKind == JsonValueKind.Object &&
                sdk.TryGetProperty("version", out var version) &&
                version.ValueKind == JsonValueKind.String &&
                NuGetVersion.TryParse(version.GetString(), out sdkVersion))
            {
                return true;
            }
        }
        catch (JsonException)
        {
            // Ignore
        }

        return false;
    }

    [ExcludeFromCodeCoverage]
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
            NuGetVersion previousVersion,
            NuGetVersion upgradedVersion);
    }
}
