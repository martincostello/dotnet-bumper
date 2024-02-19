// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class ServerlessUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<ServerlessUpgrader> logger) : FileUpgrader(console, options, logger)
{
    private const string ManagedRuntimePrefix = "dotnet";

    protected override string Action => "Upgrading Serverless";

    protected override string InitialStatus => "Update Serverless runtimes";

    protected override IReadOnlyList<string> Patterns => ["serverless.yml", "serverless.yaml"];

    protected override async Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var runtime = GetManagedRuntime(upgrade.Channel, upgrade.ReleaseType);

        if (runtime is null)
        {
            Log.LambdaRuntimeNotSupported(Logger, upgrade.Channel);
            return false;
        }

        Log.UpgradingServerlessRuntimes(logger);

        bool filesChanged = false;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            if (!TryParseServerless(path, out var yaml))
            {
                continue;
            }

            var finder = new RuntimeFinder(upgrade.Channel.Major);
            yaml.Accept(finder);

            if (finder.LineIndexes.Count > 0)
            {
                context.Status = StatusMessage($"Updating {name}...");

                await UpdateRuntimesAsync(path, runtime, finder.LineIndexes, cancellationToken);

                filesChanged = true;
            }
        }

        return filesChanged;
    }

    private static string? GetManagedRuntime(Version channel, DotNetReleaseType type)
    {
        if (channel.Major < 6)
        {
            return null;
        }

        if (type != DotNetReleaseType.Lts)
        {
            // AWS Lambda only supports stable LTS releases of .NET
            return null;
        }

        return $"{ManagedRuntimePrefix}{channel.Major}";
    }

    private bool TryParseServerless(
        string fileName,
        [NotNullWhen(true)] out YamlStream? serverless)
    {
        using var stream = File.OpenRead(fileName);
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
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);

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

        await File.WriteAllLinesAsync(path, lines, cancellationToken);

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

    private sealed class RuntimeFinder(int upgradeVersion) : YamlVisitorBase
    {
        public IList<int> LineIndexes { get; } = [];

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode { Value: "runtime" } &&
                value is YamlScalarNode { Value.Length: > 0 } node &&
                node.Value.StartsWith(ManagedRuntimePrefix, StringComparison.Ordinal))
            {
                string suffix = node.Value[ManagedRuntimePrefix.Length..];

                if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int current) &&
                    current < upgradeVersion)
                {
                    LineIndexes.Add(node.Start.Line - 1);
                }
            }

            base.VisitPair(key, value);
        }
    }
}
