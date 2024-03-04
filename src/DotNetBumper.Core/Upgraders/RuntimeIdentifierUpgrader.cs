// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class RuntimeIdentifierUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<RuntimeIdentifierUpgrader> logger) : XmlFileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading runtime identifiers";

    protected override string InitialStatus => "Update RIDs";

    protected override IReadOnlyList<string> Patterns => ["Directory.Build.props", "*.csproj", "*.fsproj", "*.pubxml"];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        if (upgrade.Channel < DotNetVersions.EightPointZero)
        {
            // RIDs only need updating if upgrading to .NET 8.0+
            return ProcessingResult.None;
        }

        Log.UpgradingRuntimeIdentifier(Logger);

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

            foreach (var property in project.Root.Elements("PropertyGroup").Elements())
            {
                if (TryUpgradeRuntimeId(property))
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
            logContext.Changelog.Add("Update runtime identifiers");
        }

        return result;
    }

    private static bool TryUpgradeRuntimeId(XElement property)
    {
        if (RuntimeIdentifierHelpers.TryUpdateRid(property.Value, out var updated) ||
            RuntimeIdentifierHelpers.TryUpdateRidInPath(property.Value, out updated))
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
            Message = "Upgrading runtime identifier.")]
        public static partial void UpgradingRuntimeIdentifier(ILogger logger);
    }
}
