// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class ServerlessUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<ServerlessUpgrader> logger) : FileUpgrader(console, options, logger)
{
    private static readonly Version MinimumVersion = new(6, 0);

    protected override string Action => "Upgrading Serverless";

    protected override string InitialStatus => "Update Serverless runtimes";

    protected override IReadOnlyList<string> Patterns => ["serverless.yml", "serverless.yaml"];

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

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            if (!TryParseServerless(path, out var yaml, out var encoding))
            {
                result = result.Max(ProcessingResult.Warning);
                continue;
            }

            var finder = new RuntimeFinder(upgrade.Channel);
            yaml.Accept(finder);

            if (finder.LineIndexes.Count > 0)
            {
                if (runtime is null)
                {
                    if (!warningEmitted)
                    {
                        Console.WriteUnsupportedLambdaRuntimeWarning(upgrade);
                        Log.LambdaRuntimeNotSupported(Logger, upgrade.Channel);
                        warningEmitted = true;
                    }

                    result = result.Max(ProcessingResult.Warning);
                    continue;
                }

                context.Status = StatusMessage($"Updating {name}...");

                await UpdateRuntimesAsync(path, runtime, finder.LineIndexes, encoding, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }
        }

        return result;
    }

    private static string? GetManagedRuntime(UpgradeInfo upgrade)
    {
        if (upgrade.Channel < MinimumVersion)
        {
            return null;
        }

        if (upgrade.ReleaseType is not DotNetReleaseType.Lts ||
            upgrade.SupportPhase < DotNetSupportPhase.Active)
        {
            // AWS Lambda only supports stable LTS releases of .NET
            return null;
        }

        return upgrade.Channel.ToLambdaRuntime();
    }

    private bool TryParseServerless(
        string fileName,
        [NotNullWhen(true)] out YamlStream? serverless,
        [NotNullWhen(true)] out FileMetadata? metadata)
    {
        using var stream = FileHelpers.OpenRead(fileName, out metadata);
        using var reader = new StreamReader(stream);

        try
        {
            var yaml = new YamlStream();
            yaml.Load(reader);

            serverless = yaml;
            return true;
        }
        catch (Exception ex)
        {
            Log.ParseServerlessFailed(logger, fileName, ex);
        }

        serverless = null;
        return false;
    }

    private async Task UpdateRuntimesAsync(
        string path,
        string runtime,
        IList<int> indexes,
        FileMetadata metadata,
        CancellationToken cancellationToken)
    {
        // TODO Use FileHelpers instead so that line endings are preserved
        var lines = await File.ReadAllLinesAsync(path, metadata.Encoding, cancellationToken);

        for (int i = 0; i < indexes.Count; i++)
        {
            string original = lines[indexes[i]];

            var updated = new StringBuilder(original.Length);

            int index = original.IndexOf(':', StringComparison.Ordinal);

            Debug.Assert(index != -1, "The runtime line should contain a colon.");

            updated.Append(original[..(index + 1)]);
            updated.Append(' ');
            updated.Append(runtime);

            // Preserve any comments
            index = original.IndexOf('#', StringComparison.Ordinal);

            if (index != -1)
            {
                updated.Append(' ');
                updated.Append(original[index..]);
            }

            lines[indexes[i]] = updated.ToString();
        }

        await File.WriteAllLinesAsync(path, lines, metadata.Encoding, cancellationToken);

        Log.UpgradedManagedRuntimes(logger, path, runtime);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading Serverless managed runtime versions.")]
        public static partial void UpgradingServerlessRuntimes(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = ".NET {Channel} is not supported by AWS Lambda.")]
        public static partial void LambdaRuntimeNotSupported(ILogger logger, Version channel);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Unable to parse Serverless file {FileName}.")]
        public static partial void ParseServerlessFailed(
            ILogger logger,
            string fileName,
            Exception exception);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Upgraded .NET managed runtimes in {FileName} to {Runtime}.")]
        public static partial void UpgradedManagedRuntimes(
            ILogger logger,
            string fileName,
            string runtime);
    }

    private sealed class RuntimeFinder(Version channel) : YamlVisitorBase
    {
        public IList<int> LineIndexes { get; } = [];

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode { Value: "runtime" } &&
                value is YamlScalarNode { Value.Length: > 0 } node &&
                node.Value.ToVersionFromLambdaRuntime() is { } version)
            {
                if (version >= MinimumVersion && version < channel)
                {
                    LineIndexes.Add(node.Start.Line - 1);
                }
            }

            base.VisitPair(key, value);
        }
    }
}
