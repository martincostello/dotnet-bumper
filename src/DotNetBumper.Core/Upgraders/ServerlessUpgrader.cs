// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
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
    protected override string Action => "Upgrading Serverless";

    protected override string InitialStatus => "Update Serverless runtimes";

    protected override IReadOnlyList<string> Patterns => ["serverless.yml", "serverless.yaml"];

    protected override Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var runtime = GetManagedRuntime(upgrade.Channel);

        if (runtime is null)
        {
            Log.LambdaRuntimeNotSupported(Logger, upgrade.Channel);
            return Task.FromResult(false);
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

            var visitor = new RuntimeUpdater(runtime);
            yaml.Accept(visitor);

            if (visitor.Edited)
            {
                context.Status = StatusMessage($"Updating {name}...");

                using var stream = File.Open(path, FileMode.Open);
                using var writer = new StreamWriter(stream);

                yaml.Save(writer, assignAnchors: false);
                filesChanged = true;

                Log.UpgradedManagedRuntimes(logger, path, runtime);
            }
        }

        return Task.FromResult(filesChanged);
    }

    private static string? GetManagedRuntime(Version channel)
    {
        return channel.Major switch
        {
            6 => "dotnet6",
            8 => "dotnet8",
            _ => null,
        };
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

    private sealed class RuntimeUpdater(string runtime) : YamlVisitorBase
    {
        public bool Edited { get; private set; }

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode { Value: "runtime" } &&
                value is YamlScalarNode runtimeValue &&
                runtimeValue.Value?.StartsWith("dotnet", StringComparison.Ordinal) is true)
            {
                runtimeValue.Value = runtime;
                Edited = true;
            }

            base.VisitPair(key, value);
        }
    }
}
