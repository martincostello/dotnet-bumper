// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class PowerShellScriptUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger<PowerShellScriptUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading PowerShell scripts";

    protected override string InitialStatus => "Update PowerShell scripts";

    protected override IReadOnlyList<string> Patterns { get; } = ["*.bash", "*.cmd", "*.ps1", "*.sh", "*.yaml", "*.yml"];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPowerShellScripts(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            result = result.Max(await TryEditScriptAsync(path, upgrade, context, cancellationToken));
        }

        return result;
    }

    private async Task<ProcessingResult> TryEditScriptAsync(
        string path,
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var name = RelativeName(path);
        YamlStream? workflow = null;

        if (Path.GetExtension(path) is ".yaml" or ".yml")
        {
            var directory = PathHelpers.Normalize(Path.GetDirectoryName(path) ?? string.Empty);

            if (!directory.EndsWith(WellKnownFileNames.GitHubActionsWorkflowsDirectory, StringComparison.OrdinalIgnoreCase) ||
                !TryParseActionsWorkflow(path, out workflow))
            {
                return ProcessingResult.None;
            }
        }

        context.Status = StatusMessage($"Parsing {name}...");

        var script = workflow is not null
            ? await PowerShellScript.TryParseAsync(workflow, path, cancellationToken)
            : await PowerShellScript.TryParseAsync(path, cancellationToken);

        if (script is null)
        {
            Log.FailedToParsePowerShellScript(logger, path);
            return ProcessingResult.None;
        }

        bool edited = script.TryUpdate(upgrade.Channel);

        if (edited)
        {
            context.Status = StatusMessage($"Updating {name}...");

            await using var output = File.OpenWrite(path);
            await using var writer = new StreamWriter(output, script.FileMetadata.Encoding, leaveOpen: true)
            {
                NewLine = script.FileMetadata.NewLine,
            };

            foreach (var line in script.Lines)
            {
                await writer.WriteAsync(line);
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync(cancellationToken);
            output.SetLength(output.Position);
        }

        return edited ? ProcessingResult.Success : ProcessingResult.None;
    }

    private bool TryParseActionsWorkflow(
        string fileName,
        [NotNullWhen(true)] out YamlStream? workflow)
    {
        try
        {
            workflow = YamlHelpers.ParseFile(fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log.ParseActionsWorkflowFailed(logger, fileName, ex);
            workflow = null;
            return false;
        }
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading PowerShell scripts.")]
        public static partial void UpgradingPowerShellScripts(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Unable to parse {FileName} as a PowerShell script.")]
        public static partial void FailedToParsePowerShellScript(ILogger logger, string fileName);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Unable to parse GitHub Actions workflow file {FileName}.")]
        public static partial void ParseActionsWorkflowFailed(
            ILogger logger,
            string fileName,
            Exception exception);
    }
}
