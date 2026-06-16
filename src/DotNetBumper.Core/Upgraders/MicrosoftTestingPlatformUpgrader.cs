// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

/// <summary>
/// An upgrader that migrates projects using Microsoft Testing Platform through the legacy
/// VSTest mode of <c>dotnet test</c> to the dedicated Microsoft Testing Platform mode. This
/// is required because running tests via the VSTest target is not supported by Microsoft
/// Testing Platform when using the .NET 10 SDK or later.
/// See <c>https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test</c>.
/// </summary>
internal sealed partial class MicrosoftTestingPlatformUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<MicrosoftTestingPlatformUpgrader> logger) : XmlFileUpgrader(console, environment, options, logger)
{
    private const string RunnerName = "Microsoft.Testing.Platform";
    private const string VSTestSupportProperty = "TestingPlatformDotnetTestSupport";

    private static readonly string[] ObsoleteProperties =
    [
        VSTestSupportProperty,
        "TestingPlatformCaptureOutput",
        "TestingPlatformShowTestsFailure",
    ];

    protected override string Action => "Migrating to Microsoft Testing Platform";

    protected override string InitialStatus => "Migrate test platform";

    protected override IReadOnlyList<string> Patterns { get; } =
    [
        WellKnownFileNames.DirectoryBuildProps,
        WellKnownFileNames.CSharpProjects,
        WellKnownFileNames.FSharpProjects,
        WellKnownFileNames.VisualBasicProjects,
    ];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var projects = new List<(string Path, XDocument Document, FileMetadata? Metadata)>(fileNames.Count);
        bool usesVSTestMode = false;

        foreach (var filePath in fileNames)
        {
            var name = RelativeName(filePath);
            context.Status = StatusMessage($"Parsing {name}...");

            (var project, var metadata) = await LoadProjectAsync(filePath, cancellationToken);

            if (project?.Root is null)
            {
                continue;
            }

            projects.Add((filePath, project, metadata));

            if (UsesVSTestMode(project.Root))
            {
                usesVSTestMode = true;
            }
        }

        if (!usesVSTestMode)
        {
            // The project(s) do not use Microsoft Testing Platform through the legacy
            // VSTest mode of "dotnet test", so there is nothing that needs to be migrated.
            return ProcessingResult.None;
        }

        Log.MigratingTestPlatform(Logger);

        var result = ProcessingResult.None;

        foreach ((var filePath, var project, var metadata) in projects)
        {
            if (TryRemoveObsoleteProperties(project.Root!))
            {
                context.Status = StatusMessage($"Updating {RelativeName(filePath)}...");

                await UpdateProjectAsync(filePath, project, metadata, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }
        }

        result = result.Max(await EnableTestingPlatformRunnerAsync(upgrade, context, cancellationToken));

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add("Migrate test projects to use Microsoft Testing Platform");
        }

        return result;
    }

    private static bool UsesVSTestMode(XElement project)
    {
        foreach (var property in ProjectHelpers.EnumerateProperties(project))
        {
            if (string.Equals(property.Name.LocalName, VSTestSupportProperty, StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(property.Value, out var enabled) &&
                enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryRemoveObsoleteProperties(XElement project)
    {
        var properties = ProjectHelpers.EnumerateProperties(project)
            .Where((p) => ObsoleteProperties.Any((name) => string.Equals(p.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var property in properties)
        {
            // Also remove the whitespace preceding the property to avoid leaving a blank line behind.
            if (property.PreviousNode is XText text && string.IsNullOrWhiteSpace(text.Value))
            {
                text.Remove();
            }

            property.Remove();
        }

        return properties.Count > 0;
    }

    private static bool TryEnableRunner(JsonObject root)
    {
        if (root["test"] is not JsonObject test)
        {
            test = [];
            root["test"] = test;
        }

        if (test["runner"] is JsonValue runner &&
            runner.GetValueKind() is JsonValueKind.String &&
            string.Equals(runner.GetValue<string>(), RunnerName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        test["runner"] = RunnerName;
        return true;
    }

    private async Task<ProcessingResult> EnableTestingPlatformRunnerAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var globalJsonFiles = Directory.GetFiles(Options.ProjectPath, WellKnownFileNames.GlobalJson, SearchOption.AllDirectories);

        if (globalJsonFiles.Length is 0)
        {
            // A global.json file is required to opt in to the Microsoft Testing Platform mode of
            // "dotnet test", so create one configured to use the runner if none already exists.
            var path = Path.Combine(Options.ProjectPath, WellKnownFileNames.GlobalJson);

            context.Status = StatusMessage($"Creating {RelativeName(path)}...");

            var globalJson = new JsonObject()
            {
                ["sdk"] = new JsonObject() { ["version"] = upgrade.SdkVersion.ToString() },
                ["test"] = new JsonObject() { ["runner"] = RunnerName },
            };

            // The file does not exist yet, so it cannot be saved using JsonExtensions.SaveAsync()
            // as that reads the metadata of the existing file to preserve its encoding.
            var json = globalJson.ToJsonString(new() { WriteIndented = true });
            await File.WriteAllTextAsync(path, json + Environment.NewLine, cancellationToken);

            return ProcessingResult.Success;
        }

        var result = ProcessingResult.None;

        foreach (var path in globalJsonFiles)
        {
            JsonObject? root;

            try
            {
                if (!JsonHelpers.TryLoadObject(path, out root))
                {
                    result = result.Max(ProcessingResult.Warning);
                    continue;
                }
            }
            catch (JsonException)
            {
                result = result.Max(ProcessingResult.Warning);
                continue;
            }

            if (TryEnableRunner(root))
            {
                context.Status = StatusMessage($"Updating {RelativeName(path)}...");

                await root.SaveAsync(path, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Migrating test projects to use Microsoft Testing Platform.")]
        public static partial void MigratingTestPlatform(ILogger logger);
    }
}
