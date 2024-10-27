// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal abstract partial class AwsLambdaUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger logger) : FileUpgrader(console, environment, options, logger)
{
    protected static readonly Version MinimumVersion = DotNetVersions.SixPointZero;

    protected static string? GetManagedRuntime(UpgradeInfo upgrade)
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

    protected static bool? IsSupportedRuntime(string runtime, UpgradeInfo upgrade)
    {
        var version = runtime.ToVersionFromLambdaRuntime();

        if (version is { } && version < upgrade.Channel)
        {
            if (upgrade.SupportPhase < DotNetSupportPhase.Active ||
                upgrade.ReleaseType != DotNetReleaseType.Lts)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        return null;
    }

    protected static async Task UpdateRuntimesAsync(
        string path,
        string runtime,
        YamlRuntimeFinder finder,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var buffered = new MemoryStream();

        using (var input = FileHelpers.OpenRead(path, out var metadata))
        using (var reader = new StreamReader(input, metadata.Encoding))
        using (var writer = new StreamWriter(buffered, metadata.Encoding, leaveOpen: true))
        {
            writer.NewLine = metadata.NewLine;

            int i = 0;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (finder.LineIndexes.Contains(i++))
                {
                    var updated = new StringBuilder(line.Length);

                    int index = line.IndexOf(':', StringComparison.Ordinal);

                    Debug.Assert(index != -1, $"The {finder.PropertyName} line should contain a colon.");

                    updated.Append(line[..(index + 1)]);
                    updated.Append(' ');
                    updated.Append(runtime);

                    // Preserve any comments
                    index = line.IndexOf('#', StringComparison.Ordinal);

                    if (index != -1)
                    {
                        updated.Append(' ');
                        updated.Append(line[index..]);
                    }

                    line = updated.ToString();
                }

                await writer.WriteAsync(line);
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }

        buffered.Seek(0, SeekOrigin.Begin);

        await using var output = File.OpenWrite(path);

        await buffered.CopyToAsync(output, cancellationToken);
        await buffered.FlushAsync(cancellationToken);

        buffered.SetLength(buffered.Position);

        Log.UpgradedManagedRuntimes(logger, path, runtime);
    }

    protected void LogUnsupportedRuntime(UpgradeInfo upgrade)
    {
        Console.WriteUnsupportedLambdaRuntimeWarning(upgrade);
        Log.LambdaRuntimeNotSupported(Logger, upgrade.Channel);
    }

    protected bool TryLoadJsonObject(string path, [NotNullWhen(true)] out JsonObject? configuration)
    {
        configuration = null;

        try
        {
            if (!JsonHelpers.TryLoadObject(path, out configuration))
            {
                return false;
            }

            return configuration is not null;
        }
        catch (JsonException ex)
        {
            Log.ParseJsonObjectFailed(Logger, path, ex);
            return false;
        }
    }

    protected sealed class YamlRuntimeFinder(string propertyName, Version channel) : YamlVisitorBase
    {
        public IList<int> LineIndexes { get; } = [];

        public string PropertyName { get; } = propertyName;

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode keyName &&
                keyName.Value == PropertyName &&
                value is YamlScalarNode { Value.Length: > 0 } node &&
                node.Value.ToVersionFromLambdaRuntime() is { } version &&
                version >= MinimumVersion && version < channel)
            {
                LineIndexes.Add((int)node.Start.Line - 1);
            }

            base.VisitPair(key, value);
        }
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Unable to parse JSON object from file {FileName}.")]
        public static partial void ParseJsonObjectFailed(
            ILogger logger,
            string fileName,
            Exception exception);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Upgraded .NET managed runtimes in {FileName} to {Runtime}.")]
        public static partial void UpgradedManagedRuntimes(
            ILogger logger,
            string fileName,
            string runtime);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = ".NET {Channel} is not supported by AWS Lambda.")]
        public static partial void LambdaRuntimeNotSupported(ILogger logger, Version channel);
    }
}
