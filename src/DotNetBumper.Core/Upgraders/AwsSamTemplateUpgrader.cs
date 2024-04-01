// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsSamTemplateUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<AwsSamTemplateUpgrader> logger) : AwsLambdaUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading AWS SAM templates";

    protected override string InitialStatus => "Update AWS SAM templates";

    protected override IReadOnlyList<string> Patterns { get; } =
    [
        WellKnownFileNames.AwsLambdaToolsDefaults,
        "*.json",
        "*.yml",
        "*.yaml"
    ];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingAwsSamTemplates(logger);

        fileNames = GetTemplatePaths(fileNames);

        if (fileNames.Count is 0)
        {
            return ProcessingResult.None;
        }

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
            logContext.Changelog.Add($"Update AWS SAM template Lambda runtime to `{runtime}`");
        }

        return result;
    }

    private List<string> GetTemplatePaths(IReadOnlyList<string> filePaths)
    {
        var templates = new List<string>();
        var toolsDefaults = new List<string>();

        foreach (var path in filePaths)
        {
            var fileName = Path.GetFileName(path);

            if (!string.Equals(fileName, WellKnownFileNames.AwsLambdaToolsDefaults, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            toolsDefaults.Add(path);

            if (!TryLoadJsonObject(path, out var document))
            {
                continue;
            }

            if (document is not null &&
                document.TryGetStringProperty("template", out _, out var templateFile) &&
                !string.IsNullOrWhiteSpace(templateFile))
            {
                // The template file could be either relative to the tools defaults file or the project path
                string[] candidates =
                [
                    Path.Combine(Path.GetDirectoryName(path)!, templateFile),
                    Path.Combine(Options.ProjectPath, templateFile)
                ];

                foreach (var candidate in candidates)
                {
                    var templatePath = Path.GetFullPath(Path.Combine(candidate, templateFile));

                    if (File.Exists(templatePath))
                    {
                        templates.Add(templatePath);
                    }
                }
            }
        }

        var allTemplates = filePaths
            .Except(toolsDefaults, StringComparer.OrdinalIgnoreCase)
            .Union(templates, StringComparer.OrdinalIgnoreCase)
            .Where((p) => !IsInAwsSamBuildDirectory(p))
            .Distinct();

        return [.. allTemplates];

        static bool IsInAwsSamBuildDirectory(string path)
            => path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).Contains(".aws-sam");
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

        if (!TryParseSamTemplate(path, out var template))
        {
            return (ProcessingResult.Warning, false);
        }

        context.Status = StatusMessage($"Updating {name}...");
        return await template.TryUpgradeAsync(runtime, upgrade, logger, cancellationToken);
    }

    private bool TryParseSamTemplate(
        string fileName,
        [NotNullWhen(true)] out SamTemplate? template)
    {
        return Path.GetExtension(fileName) switch
        {
            ".json" => TryParseSamJsonTemplate(fileName, out template),
            _ => TryParseSamYamlTemplate(fileName, out template),
        };
    }

    private bool TryParseSamJsonTemplate(
        string fileName,
        [NotNullWhen(true)] out SamTemplate? template)
    {
        template = null;

        if (!TryLoadJsonObject(fileName, out var document))
        {
            return false;
        }

        template = new JsonSamTemplate(fileName, document);
        return template.IsValid();
    }

    private bool TryParseSamYamlTemplate(
        string fileName,
        [NotNullWhen(true)] out SamTemplate? template)
    {
        template = null;

        try
        {
            var stream = YamlHelpers.ParseFile(fileName);

            if (stream is null)
            {
                return false;
            }

            template = new YamlSamTemplate(fileName, stream);
            return template.IsValid();
        }
        catch (Exception ex)
        {
            Log.ParseTemplateYamlFailed(logger, fileName, ex);
            return false;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Upgrading AWS SAM templates.")]
        public static partial void UpgradingAwsSamTemplates(ILogger logger);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Unable to parse AWS SAM YAML template file {FileName}.")]
        public static partial void ParseTemplateYamlFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
