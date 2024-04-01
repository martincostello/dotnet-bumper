// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class ServerlessUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<ServerlessUpgrader> logger) : AwsLambdaUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading Serverless";

    protected override string InitialStatus => "Update Serverless runtimes";

    protected override IReadOnlyList<string> Patterns { get; } = ["serverless.yml", "serverless.yaml"];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingServerlessRuntimes(logger);

        bool warningEmitted = false;
        var runtime = GetManagedRuntime(upgrade);

        var result = ProcessingResult.None;
        var edited = false;

        foreach (var path in fileNames)
        {
            (var updateResult, var unsupported) = await TryUpgradeAsync(path, runtime, upgrade, context, cancellationToken);

            if (unsupported && !warningEmitted)
            {
                LogUnsupportedRuntime(upgrade);
                warningEmitted = true;
            }

            edited |= updateResult is ProcessingResult.Success;
            result = result.Max(updateResult);
        }

        if (edited && runtime is { })
        {
            logContext.Changelog.Add($"Update AWS Lambda runtime to `{runtime}`");
        }

        return result;
    }

    private async Task<(ProcessingResult Result, bool UnsupportedRuntime)> TryUpgradeAsync(
        string path,
        string? runtime,
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var name = RelativeName(path);

        context.Status = StatusMessage($"Parsing {name}...");

        if (!TryParseServerless(path, out var yaml))
        {
            return (ProcessingResult.Warning, false);
        }

        var finder = new YamlRuntimeFinder("runtime", upgrade.Channel);
        yaml.Accept(finder);

        var result = ProcessingResult.None;

        if (finder.LineIndexes.Count > 0)
        {
            if (runtime is null)
            {
                return (ProcessingResult.Warning, true);
            }

            context.Status = StatusMessage($"Updating {name}...");

            await UpdateRuntimesAsync(path, runtime, finder, Logger, cancellationToken);

            result = ProcessingResult.Success;
        }

        return (result, false);
    }

    private bool TryParseServerless(
        string fileName,
        [NotNullWhen(true)] out YamlStream? serverless)
    {
        try
        {
            serverless = YamlHelpers.ParseFile(fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log.ParseServerlessFailed(logger, fileName, ex);

            serverless = null;
            return false;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Upgrading Serverless managed runtime versions.")]
        public static partial void UpgradingServerlessRuntimes(ILogger logger);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Unable to parse Serverless file {FileName}.")]
        public static partial void ParseServerlessFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
