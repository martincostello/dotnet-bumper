// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class DotNetCodeUpgrader(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<DotNetCodeUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    public override int Order => int.MaxValue; // Run after all other upgraders

    protected override string Action => "Upgrading .NET code";

    protected override string InitialStatus => "Update .NET code";

    protected override IReadOnlyList<string> Patterns { get; } =
    [
        WellKnownFileNames.SolutionFiles,
        WellKnownFileNames.CSharpProjects,
        WellKnownFileNames.FSharpProjects,
        WellKnownFileNames.VisualBasicProjects,
    ];

    protected override IReadOnlyList<string> FindFiles()
        => ProjectHelpers.FindProjectFiles(Options.ProjectPath, SearchOption);

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDotNetCode(Logger);

        var diagnostics = new Dictionary<string, int>();
        var result = ProcessingResult.None;

        foreach (var fileName in fileNames)
        {
            var relativePath = PathHelpers.Normalize(RelativeName(fileName));
            context.Status = StatusMessage($"Updating .NET code for {relativePath}...");

            (var fileResult, var fixes) = await ApplyFixesAsync(
                fileName,
                upgrade.SdkVersion,
                cancellationToken);

            foreach ((var diagnosticId, var count) in fixes)
            {
                if (!diagnostics.TryGetValue(diagnosticId, out var existing))
                {
                    existing = 0;
                }

                diagnostics[diagnosticId] = existing + count;
            }

            result = result.Max(fileResult);
        }

        foreach ((var diagnosticId, var count) in diagnostics.Where((p) => p.Value is not 0).OrderBy((p) => p.Key))
        {
            logContext.Changelog.Add($"Fix {diagnosticId} warning{(count is 1 ? string.Empty : "s")}");
        }

        return result;
    }

    private async Task<(ProcessingResult Result, Dictionary<string, int> Fixes)> ApplyFixesAsync(
        string projectOrSolution,
        NuGetVersion sdkVersion,
        CancellationToken cancellationToken)
    {
        var workingDirectory = Path.GetDirectoryName(projectOrSolution)!;

        using var globalJson = await PatchedGlobalJsonFile.TryPatchAsync(workingDirectory, sdkVersion, cancellationToken);
        using var tempDirectory = new TemporaryDirectory();

        // See https://learn.microsoft.com/dotnet/core/tools/dotnet-format#analyzers
        string[] arguments =
        [
            "format",
            "analyzers",
            "--no-restore",
            "--report",
            tempDirectory.Path,
            "--severity",
            "warn",
            "--verbosity",
            Logger.GetMSBuildVerbosity(),
        ];

        var environmentVariables = new Dictionary<string, string?>()
        {
            [WellKnownEnvironmentVariables.MSBuildSdksPath] = null,
            [WellKnownEnvironmentVariables.NoWarn] = "CA1515", // HACK Ignore CA1515 from .NET 9 as it seems to just break things
        };

        MSBuildHelper.TryAddSdkProperties(environmentVariables, globalJson?.SdkVersion ?? sdkVersion.ToString());

        var formatResult = await dotnet.RunAsync(
            workingDirectory,
            arguments,
            environmentVariables,
            cancellationToken);

        Dictionary<string, int> fixes = [];
        ProcessingResult result;

        if (!string.IsNullOrWhiteSpace(formatResult.StandardError))
        {
            Log.DotNetFormatErrors(logger, formatResult.StandardError.TrimEnd());
        }

        if (formatResult.Success)
        {
            int fixCount = 0;
            string reportPath = Path.Combine(tempDirectory.Path, "format-report.json");

            if (File.Exists(reportPath))
            {
                fixes = await GetFixesAsync(reportPath, cancellationToken);
                fixCount += fixes.Sum((p) => p.Value);
            }

            result = fixCount is 0 ? ProcessingResult.None : ProcessingResult.Success;
        }
        else
        {
            string[] warnings =
            [
                $"Failed to apply .NET code updates for {RelativeName(workingDirectory)}.",
                $"dotnet format exited with code {formatResult.ExitCode}.",
            ];

            Console.WriteLine();

            foreach (var warning in warnings)
            {
                Console.WriteWarningLine(warning);
                logContext.Warnings.Add(warning);
            }

            result = ProcessingResult.Warning;
        }

        return (result, fixes);
    }

    private async Task<Dictionary<string, int>> GetFixesAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var report = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var fixes = new Dictionary<string, int>();

        if (report.RootElement.ValueKind is JsonValueKind.Array)
        {
            List<DiagnosticFix> diagnostics = [];

            foreach (var document in report.RootElement.EnumerateArray())
            {
                if (!document.TryGetProperty("FileChanges", out var fileChanges) ||
                    fileChanges.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                var filePath = document.GetProperty("FilePath").GetString();
                var relativePath = RelativeName(filePath!);

                foreach (var change in fileChanges.EnumerateArray())
                {
                    var diagnosticId = change.GetProperty("DiagnosticId").GetString()!;
                    var lineNumber = change.GetProperty("LineNumber").GetInt32();

                    diagnostics.Add(new(relativePath, diagnosticId, lineNumber));
                }
            }

            // The diagnostics from the report are grouped to prevent duplication when multi-targeting
            foreach ((var filePath, var diagnosticId, var lineNumber) in diagnostics.Distinct())
            {
                Log.FixedDiagnostic(Logger, diagnosticId, filePath, lineNumber);

                if (!fixes.TryGetValue(diagnosticId, out var count))
                {
                    count = 0;
                }

                fixes[diagnosticId] = count + 1;
            }
        }

        return fixes;
    }

    private record struct DiagnosticFix(string FilePath, string DiagnosticId, int LineNumber);

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading .NET code.")]
        public static partial void UpgradingDotNetCode(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "dotnet format stderr: {StdErr}")]
        public static partial void DotNetFormatErrors(ILogger logger, string stderr);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Fixed diagnostic {DiagnosticId} in {FileName}:{LineNumber}.")]
        public static partial void FixedDiagnostic(ILogger logger, string diagnosticId, string fileName, int lineNumber);
    }
}
