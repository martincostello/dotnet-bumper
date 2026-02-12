// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class ContainerBaseImageUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<ContainerBaseImageUpgrader> logger) : XmlFileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading container base images";

    protected override string InitialStatus => "Update container base images";

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
        Log.UpgradingContainerBaseImage(Logger);

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
                if (TryUpgradeContainerBaseImage(property, upgrade.Channel))
                {
                    edited = true;
                }
                else if (TryUpgradeContainerFamily(property, upgrade.Channel))
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
            logContext.Changelog.Add("Update container base images");
        }

        return result;
    }

    private static bool TryUpgradeContainerBaseImage(XElement property, Version channel)
    {
        if (property.Name == (property.GetDefaultNamespace() + "ContainerBaseImage") &&
            DockerfileUpgrader.TryUpdateImage(property.Value, channel, out var updated))
        {
            property.SetValue(updated);
            return true;
        }

        return false;
    }

    private static bool TryUpgradeContainerFamily(XElement property, Version channel)
    {
        if (property.Name == (property.GetDefaultNamespace() + "ContainerFamily"))
        {
            var updated = LinuxDistros.TryUpdateDistro(channel, property.Value);

            if (!updated.SequenceEqual(property.Value))
            {
                property.SetValue(new string(updated));
                return true;
            }
        }

        return false;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Upgrading container base image.")]
        public static partial void UpgradingContainerBaseImage(ILogger logger);
    }
}
