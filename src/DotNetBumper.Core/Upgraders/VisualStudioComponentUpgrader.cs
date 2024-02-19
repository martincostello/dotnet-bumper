// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class VisualStudioComponentUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<VisualStudioComponentUpgrader> logger) : FileUpgrader(console, options, logger)
{
    protected override string Action => "Upgrading Visual Studio configuration";

    protected override string InitialStatus => "Update Visual Studio configuration";

    protected override IReadOnlyList<string> Patterns => [".vsconfig"];

    protected override async Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingComponentConfiguration(logger);

        bool filesChanged = false;

        foreach (var path in fileNames)
        {
            var name = RelativeName(path);

            context.Status = StatusMessage($"Parsing {name}...");

            if (!TryEditConfiguration(path, upgrade.Channel, out var configuration))
            {
                continue;
            }

            context.Status = StatusMessage($"Updating {name}...");

            await UpdateConfigurationAsync(path, configuration, cancellationToken);

            filesChanged = true;
        }

        return filesChanged;
    }

    private static async Task UpdateConfigurationAsync(string path, JsonObject configuration, CancellationToken cancellationToken)
    {
        using var stream = File.OpenWrite(path);
        using var writer = new Utf8JsonWriter(stream, new() { Indented = true });

        configuration.WriteTo(writer);
        await writer.FlushAsync(cancellationToken);

        await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken);
    }

    private bool TryEditConfiguration(string path, Version channel, [NotNullWhen(true)] out JsonObject? configuration)
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

        if (!configuration.TryGetPropertyValue("components", out var components) ||
            components is not JsonArray array)
        {
            return false;
        }

        const string ComponentPrefix = "Microsoft.NetCore.Component.Runtime.";

        List<int> indexes = [];

        for (int i = 0; i < array.Count; i++)
        {
            var item = array[i];

            if (item is not JsonValue value ||
                value.GetValueKind() != JsonValueKind.String)
            {
                continue;
            }

            string id = value.GetValue<string>();

            if (id.StartsWith(ComponentPrefix, StringComparison.Ordinal) &&
                Version.TryParse(id[ComponentPrefix.Length..], out var version) &&
                version < channel)
            {
                indexes.Add(i);
            }
        }

        bool updated = false;
        string component = $"{ComponentPrefix}{channel.ToString(2)}";

        if (indexes.Count is 1)
        {
            // Replace the existing item
            array[indexes[0]] = component;
            updated = true;
        }
        else if (indexes.Count > 1)
        {
            // Add the new item after the last existing item
            array.Insert(indexes[^1] + 1, component);
            updated = true;
        }

        return updated;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading Visual Studio component configuration.")]
        public static partial void UpgradingComponentConfiguration(ILogger logger);

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
