// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

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

    protected override IReadOnlyList<string> Patterns { get; } = [WellKnownFileNames.AwsLambdaToolsDefaults, "*.yml", "*.yaml"];

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

        foreach (var path in fileNames)
        {
            (var updateResult, var unsupported) = await TryUpgradeAsync(path, runtime, upgrade, context, cancellationToken);

            if (unsupported && !warningEmitted)
            {
                LogUnsupportedRuntime(upgrade);
                warningEmitted = true;
            }

            result = result.Max(updateResult);
        }

        if (result is not ProcessingResult.None && runtime is { })
        {
            logContext.Changelog.Add($"Update AWS SAM template Lambda runtime to `{runtime}`");
        }

        return result;
    }

    private static bool IsSamTemplate(YamlStream yaml)
    {
        foreach (var document in yaml.Documents)
        {
            if (document.RootNode is not YamlMappingNode mapping)
            {
                continue;
            }

            if (mapping.Children.Any(IsAwsTemplate))
            {
                return true;
            }
        }

        return false;

        static bool IsAwsTemplate(KeyValuePair<YamlNode, YamlNode> pair)
            => pair.Key is YamlScalarNode scalar && scalar.Value is "AWSTemplateFormatVersion";
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

        if (!TryParseSamTemplate(path, out var template) || !IsSamTemplate(template))
        {
            return (ProcessingResult.Warning, false);
        }

        var finder = new YamlRuntimeFinder("Runtime", upgrade.Channel);
        template.Accept(finder);

        var result = ProcessingResult.None;

        if (finder.LineIndexes.Count > 0)
        {
            if (runtime is null)
            {
                return (ProcessingResult.Warning, true);
            }

            context.Status = StatusMessage($"Updating {name}...");

            await UpdateRuntimesAsync(path, runtime, finder, cancellationToken);

            result = ProcessingResult.Success;
        }

        return (result, false);
    }

    private bool TryParseSamTemplate(
        string fileName,
        [NotNullWhen(true)] out YamlStream? template)
    {
        try
        {
            template = YamlHelpers.ParseFile(fileName);
            return template is not null;
        }
        catch (Exception ex)
        {
            Log.ParseAwsSamTemplateFailed(logger, fileName, ex);

            template = null;
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
            Message = "Unable to parse AWS SAM template file {FileName}.")]
        public static partial void ParseAwsSamTemplateFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
