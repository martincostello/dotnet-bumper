﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
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

            if (!TryEditDefaults(path, upgrade.Channel, out var configuration))
            {
                continue;
            }

            context.Status = StatusMessage($"Updating {name}...");

            await UpdateConfigurationAsync(path, configuration, cancellationToken);

            result = UpgradeResult.Success;
        }

        return result;
    }

    private static async Task UpdateConfigurationAsync(string path, JsonObject defaults, CancellationToken cancellationToken)
    {
        using var stream = File.OpenWrite(path);
        using var writer = new Utf8JsonWriter(stream, new() { Indented = true });

        defaults.WriteTo(writer);
        await writer.FlushAsync(cancellationToken);

        await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
    }

    private bool TryEditDefaults(string path, Version channel, [NotNullWhen(true)] out JsonObject? configuration)
    {
        configuration = null;

        try
        {
            using var stream = File.OpenRead(path);
            configuration = JsonNode.Parse(stream) as JsonObject;

            if (configuration is null)
            {
                return false;
            }
        }
        catch (JsonException ex)
        {
            Log.ParseConfigurationFailed(logger, path, ex);
            return false;
        }

        bool updated = false;

        if (configuration.TryGetPropertyValue("framework", out var framework) &&
            framework?.GetValueKind() is JsonValueKind.String)
        {
            string value = framework.GetValue<string>();
            var version = value.ToVersionFromTargetFramework();

            if (version is { } && version < channel)
            {
                framework.ReplaceWith(JsonValue.Create(channel.ToTargetFramework()));
                updated = true;
            }
        }

        if (configuration.TryGetPropertyValue("function-runtime", out var runtime) &&
            runtime?.GetValueKind() is JsonValueKind.String)
        {
            string value = runtime.GetValue<string>();
            var runtimeVersion = value.ToVersionFromLambdaRuntime();

            if (runtimeVersion is { } && runtimeVersion < channel)
            {
                runtime.ReplaceWith(JsonValue.Create(channel.ToLambdaRuntime()));
                updated = true;
            }
        }

        return updated;
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
