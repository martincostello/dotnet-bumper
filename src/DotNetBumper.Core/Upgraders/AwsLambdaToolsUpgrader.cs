﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsLambdaToolsUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<AwsLambdaToolsUpgrader> logger) : FileUpgrader(console, options, logger)
{
    protected override string Action => "Upgrading AWS Lambda Tools defaults";

    protected override string InitialStatus => "Update AWS Lambda Tools";

    protected override IReadOnlyList<string> Patterns => ["aws-lambda-tools-defaults.json"];

    protected override async Task<UpgradeResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingAwsLambdaTools(logger);

        UpgradeResult result = UpgradeResult.None;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            var editResult = TryEditDefaults(path, upgrade, out var configuration);

            if (editResult is UpgradeResult.Success && configuration is { })
            {
                context.Status = StatusMessage($"Updating {name}...");

                await configuration.SaveAsync(path, cancellationToken);

                result = result.Max(editResult);
            }

            result = result.Max(editResult);
        }

        return result;
    }

    private UpgradeResult TryEditDefaults(string path, UpgradeInfo upgrade, [NotNullWhen(true)] out JsonObject? configuration)
    {
        configuration = null;

        try
        {
            if (!JsonHelpers.TryLoadObject(path, out configuration))
            {
                return UpgradeResult.Warning;
            }
        }
        catch (JsonException ex)
        {
            Log.ParseConfigurationFailed(logger, path, ex);
            return UpgradeResult.Warning;
        }

        UpgradeResult result = UpgradeResult.None;

        if (configuration.TryGetStringProperty("framework", out var node, out var framework))
        {
            var version = framework.ToVersionFromTargetFramework();

            if (version is { } && version < upgrade.Channel)
            {
                node.ReplaceWith(JsonValue.Create(upgrade.Channel.ToTargetFramework()));
                result = result.Max(UpgradeResult.Success);
            }
        }

        if (configuration.TryGetStringProperty("function-runtime", out node, out var runtime))
        {
            var version = runtime.ToVersionFromLambdaRuntime();

            if (version is { } && version < upgrade.Channel)
            {
                if (upgrade.SupportPhase < DotNetSupportPhase.Active ||
                    upgrade.ReleaseType != DotNetReleaseType.Lts)
                {
                    string qualifier = upgrade.ReleaseType is DotNetReleaseType.Lts ? "yet " : string.Empty;
                    Console.MarkupLine($"[yellow]:warning: .NET {upgrade.Channel} is {qualifier}supported by AWS Lambda.[/]");
                    result = result.Max(UpgradeResult.Warning);
                }
                else
                {
                    node.ReplaceWith(JsonValue.Create(upgrade.Channel.ToLambdaRuntime()));
                    result = result.Max(UpgradeResult.Success);
                }
            }
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading AWS Lambda Tools defaults.")]
        public static partial void UpgradingAwsLambdaTools(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Unable to parse configuration file {FileName}.")]
        public static partial void ParseConfigurationFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
