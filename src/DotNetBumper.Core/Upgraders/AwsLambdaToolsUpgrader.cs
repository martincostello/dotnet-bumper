// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsLambdaToolsUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger<AwsLambdaToolsUpgrader> logger) : AwsLambdaUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading AWS Lambda Tools defaults";

    protected override string InitialStatus => "Update AWS Lambda Tools";

    protected override IReadOnlyList<string> Patterns { get; } = [WellKnownFileNames.AwsLambdaToolsDefaults];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingAwsLambdaTools(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            var editResult = await TryUpgradeAsync(path, upgrade, context, cancellationToken);
            result = result.Max(editResult);
        }

        return result;
    }

    private async Task<ProcessingResult> TryUpgradeAsync(
        string path,
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var name = RelativeName(path);

        context.Status = StatusMessage($"Parsing {name}...");

        var result = TryEditDefaults(path, upgrade, out var configuration);

        if (result is ProcessingResult.Success && configuration is { })
        {
            context.Status = StatusMessage($"Updating {name}...");
            await configuration.SaveAsync(path, cancellationToken);
        }

        return result;
    }

    private ProcessingResult TryEditDefaults(string path, UpgradeInfo upgrade, [NotNullWhen(true)] out JsonObject? configuration)
    {
        if (!TryLoadJsonObject(path, out configuration))
        {
            return ProcessingResult.Warning;
        }

        var result = ProcessingResult.None;

        if (configuration.TryGetStringProperty("framework", out var node, out var framework))
        {
            var version = framework.ToVersionFromTargetFramework();

            if (version is { } && version < upgrade.Channel)
            {
                node.ReplaceWith(JsonValue.Create(upgrade.Channel.ToTargetFramework()));
                result = result.Max(ProcessingResult.Success);
            }
        }

        if (configuration.TryGetStringProperty("function-runtime", out node, out var runtime) &&
            IsSupportedRuntime(runtime, upgrade) is { } supported)
        {
            if (supported)
            {
                node.ReplaceWith(JsonValue.Create(upgrade.Channel.ToLambdaRuntime()));
                result = result.Max(ProcessingResult.Success);
            }
            else
            {
                Console.WriteUnsupportedLambdaRuntimeWarning(upgrade);
                result = result.Max(ProcessingResult.Warning);
            }
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Upgrading AWS Lambda Tools defaults.")]
        public static partial void UpgradingAwsLambdaTools(ILogger logger);
    }
}
