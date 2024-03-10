// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class PowerShellScriptUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger<PowerShellScriptUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading PowerShell scripts";

    protected override string InitialStatus => "Update PowerShell scripts";

    protected override IReadOnlyList<string> Patterns => ["*.ps1"];

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
            result = await TryEditScriptAsync(path, upgrade, context, cancellationToken);
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

        context.Status = StatusMessage($"Parsing {name}...");

        var script = await PowerShellScript.TryParseAsync(path, cancellationToken);

        if (script is null)
        {
            // TODO Log that the script could not be parsed
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

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading PowerShell scripts.")]
        public static partial void UpgradingPowerShellScripts(ILogger logger);
    }
}
