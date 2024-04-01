// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class TargetFrameworkUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<TargetFrameworkUpgrader> logger) : XmlFileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading target frameworks";

    protected override string InitialStatus => "Update TFMs";

    protected override IReadOnlyList<string> Patterns { get; } =
    [
        WellKnownFileNames.DirectoryBuildProps,
        WellKnownFileNames.CSharpProjects,
        WellKnownFileNames.FSharpProjects,
        WellKnownFileNames.PublishProfiles,
        WellKnownFileNames.VisualBasicProjects,
    ];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(Logger);

        var result = ProcessingResult.None;

        foreach (var filePath in fileNames)
        {
            var name = RelativeName(filePath);

            context.Status = StatusMessage($"Parsing {name}...");

            (var project, var metadata) = await LoadProjectAsync(filePath, cancellationToken);

            if (project is null || project.Root is null)
            {
                result = result.Max(ProcessingResult.Warning);
                continue;
            }

            bool edited = false;

            foreach (var property in ProjectHelpers.EnumerateProperties(project.Root))
            {
                if (TryUpgradeTargetFramework(property, upgrade.Channel))
                {
                    edited = true;
                }
            }

            if (edited)
            {
                context.Status = StatusMessage($"Updating {name}...");

                await UpdateProjectAsync(filePath, project, metadata, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update target framework to `{upgrade.Channel.ToTargetFramework()}`");
        }

        return result;
    }

    private static bool TryUpgradeTargetFramework(XElement property, Version channel)
    {
        if (TargetFrameworkHelpers.TryUpdateTfm(property.Value, channel, out var updated) ||
            TargetFrameworkHelpers.TryUpdateTfmInPath(property.Value, channel, out updated))
        {
            property.SetValue(updated);
            return true;
        }

        return false;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Upgrading target framework moniker.")]
        public static partial void UpgradingTargetFramework(ILogger logger);
    }
}
