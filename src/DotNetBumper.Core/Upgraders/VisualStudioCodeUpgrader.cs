// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class VisualStudioCodeUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<VisualStudioCodeUpgrader> logger) : FileUpgrader(console, options, logger)
{
    private static readonly char[] PathSeparators = ['\\', '/'];

    protected override string Action => "Upgrading Visual Studio Code configuration";

    protected override string InitialStatus => "Update Visual Studio Code configuration";

    protected override IReadOnlyList<string> Patterns => ["launch.json"];

    protected override IReadOnlyList<string> FindFiles()
    {
        return base.FindFiles()
                   .Where(IsVSCodeConfig)
                   .ToList();

        static bool IsVSCodeConfig(string path)
        {
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileName(directory);
            return name is ".vscode";
        }
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingConfiguration(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            if (!TryUpdateTargetFrameworks(path, upgrade.Channel, out var configuration))
            {
                continue;
            }

            context.Status = StatusMessage($"Updating {name}...");

            await UpdateConfigurationAsync(path, configuration, cancellationToken);

            result = ProcessingResult.Success;
        }

        return result;
    }

    private static async Task UpdateConfigurationAsync(string path, JsonObject configuration, CancellationToken cancellationToken)
    {
        var options = new JsonWriterOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = true,
        };

        await configuration.SaveAsync(path, options, cancellationToken);
    }

    private bool TryUpdateTargetFrameworks(string path, Version channel, [NotNullWhen(true)] out JsonObject? configuration)
    {
        configuration = null;

        try
        {
            if (!JsonHelpers.TryLoadObject(path, out configuration))
            {
                return false;
            }
        }
        catch (JsonException ex)
        {
            Log.ParseJsonFailed(logger, path, ex);
            return false;
        }

        return UpdateStringNodes(configuration, channel, TryUpdatePathTfm);

        static bool TryUpdatePathTfm(JsonValue node, Version channel)
        {
            Debug.Assert(node.GetValueKind() is JsonValueKind.String, $"JSON value {node.GetPath()} is not a string. Kind: {node.GetValueKind()}");

            string value = node.GetValue<string>();

            if (value.Split(PathSeparators).Any((p) => p.IsTargetFrameworkMoniker()))
            {
                var builder = new StringBuilder();
                var remaining = value.AsSpan();

                bool updated = false;

                while (!remaining.IsEmpty)
                {
                    int index = remaining.IndexOfAny(PathSeparators);

                    if (index < 0)
                    {
                        builder.Append(remaining);
                        break;
                    }

                    string segment = new(remaining[..index]);

                    if (segment.IsTargetFrameworkMoniker())
                    {
                        if (segment.ToVersionFromTargetFramework() is { } version && version < channel)
                        {
                            segment = channel.ToTargetFramework();
                            updated = true;
                        }
                    }

                    builder.Append(segment)
                           .Append(remaining.Slice(index, 1));

                    remaining = remaining[(index + 1)..];
                }

                if (updated)
                {
                    Debug.Assert(builder.Length > 0, "The builder should have a length.");
                    Debug.Assert(builder.ToString() != value, "The value is was not updated.");

                    node.ReplaceWith(JsonValue.Create(builder.ToString()));
                    return true;
                }
            }

            return false;
        }

        static bool UpdateStringNodes(JsonObject root, Version channel, Func<JsonValue, Version, bool> processValue)
        {
            return Visit(root, channel, processValue);

            static bool Visit(JsonObject node, Version channel, Func<JsonValue, Version, bool> processValue)
            {
                bool edited = false;

                foreach (var property in node.ToArray())
                {
                    if (property.Value is JsonValue value)
                    {
                        if (value.GetValueKind() is JsonValueKind.String)
                        {
                            edited |= processValue(value, channel);
                        }
                    }
                    else if (property.Value is JsonArray array)
                    {
                        foreach (var item in array)
                        {
                            if (item is JsonObject obj)
                            {
                                edited |= Visit(obj, channel, processValue);
                            }
                        }
                    }
                    else if (property.Value is JsonObject obj)
                    {
                        edited |= Visit(obj, channel, processValue);
                    }
                }

                return edited;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading Visual Studio Code configuration.")]
        public static partial void UpgradingConfiguration(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Unable to parse JSON file {FileName}.")]
        public static partial void ParseJsonFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
