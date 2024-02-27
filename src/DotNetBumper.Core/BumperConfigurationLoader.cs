// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MartinCostello.DotNetBumper;

internal partial class BumperConfigurationLoader(
    IOptions<UpgradeOptions> options,
    ILogger<BumperConfigurationLoader> logger)
{
    public virtual async Task<BumperConfiguration?> LoadAsync(CancellationToken cancellationToken)
    {
        (var fileName, bool isJson, bool isRequired) = FindConfiguration();

        BumperConfiguration? configuration = null;

        if (fileName is not null)
        {
            configuration = isJson ? await DeserializeJsonAsync(fileName, cancellationToken) : DeserializeYaml(fileName);

            if (configuration is null && isRequired)
            {
                throw new InvalidOperationException($"The configuration file '{fileName}' could not be loaded. Is the file valid JSON or YAML?");
            }
        }

        return configuration;
    }

    private (string? Path, bool IsJson, bool IsRequired) FindConfiguration()
    {
        if (options.Value.ConfigurationPath is { } userConfig)
        {
            if (!File.Exists(userConfig))
            {
                throw new FileNotFoundException("The specified custom configuration file could not be found.", userConfig);
            }

            bool isJson = string.Equals(Path.GetExtension(userConfig) ?? "json", "json", StringComparison.OrdinalIgnoreCase);
            return (userConfig, isJson, true);
        }

        var path = options.Value.ProjectPath;

        var fileNames = new[]
        {
            (Path.Combine(path, ".dotnet-bumper.json"), true),
            (Path.Combine(path, ".dotnet-bumper.yml"), false),
            (Path.Combine(path, ".dotnet-bumper.yaml"), false),
        };

        foreach ((var fileName, bool isJson) in fileNames)
        {
            if (File.Exists(fileName))
            {
                return (fileName, isJson, false);
            }
        }

        return (null, true, false);
    }

    private async Task<BumperConfiguration?> DeserializeJsonAsync(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(fileName);
            return await JsonSerializer.DeserializeAsync(stream, BumperJsonSerializerContext.Default.BumperConfiguration, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.FailedToLoadJson(logger, fileName, ex);
            return null;
        }
    }

    private BumperConfiguration? DeserializeYaml(string fileName)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        using var stream = File.OpenRead(fileName);
        using var reader = new StreamReader(stream);

        try
        {
            return deserializer.Deserialize<BumperConfiguration>(reader);
        }
        catch (Exception ex)
        {
            Log.FailedToLoadYaml(logger, fileName, ex);
            return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Failed to load JSON configuration file {FileName}.")]
        public static partial void FailedToLoadJson(ILogger logger, string fileName, Exception exception);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Failed to load YAML configuration file {FileName}.")]
        public static partial void FailedToLoadYaml(ILogger logger, string fileName, Exception exception);
    }
}
