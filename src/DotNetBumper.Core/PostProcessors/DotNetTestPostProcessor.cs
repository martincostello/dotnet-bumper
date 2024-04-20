// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.PostProcessors;

internal sealed partial class DotNetTestPostProcessor(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IEnvironment environment,
    BumperConfigurationProvider configurationProvider,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<DotNetTestPostProcessor> logger) : PostProcessor(console, environment, options, logger)
{
    protected override string Action => "Running tests";

    protected override string InitialStatus => "Test project";

    protected override Style? SpinnerStyle { get; } = Style.Parse("green");

    protected override Color StatusColor => Color.Teal;

    public override async Task<ProcessingResult> PostProcessAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        if (!Options.TestUpgrade)
        {
            return ProcessingResult.None;
        }

        return await base.PostProcessAsync(upgrade, cancellationToken);
    }

    protected override async Task<ProcessingResult> PostProcessCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var projects = ProjectHelpers.FindProjects(Options.ProjectPath);

        if (projects.Count is 0)
        {
            Console.WriteWarningLine("Could not find any test projects.");
            Console.WriteWarningLine("The project may not be in a working state.");

            return ProcessingResult.Warning;
        }
        else
        {
            var result = await RunTestsAsync(upgrade.SdkVersion.ToString(), projects, context, cancellationToken);

            logContext.Add(result);

            Console.WriteLine();

            if (result.Success)
            {
                Console.WriteSuccessLine("Upgrade successfully tested.");
            }
            else
            {
                Console.WriteWarningLine("The project upgrade did not result in a successful test run.");
                Console.WriteWarningLine("The project may not be in a working state.");

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    Console.WriteLine();
                    Console.WriteProgressLine(TaskEnvironment, result.StandardError);
                }

                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    Console.WriteLine();
                    Console.WriteProgressLine(TaskEnvironment, result.StandardOutput);
                }

                if (result.BuildLogs?.Summary?.Count > 0)
                {
                    WriteBuildLogs(result.BuildLogs.Summary);
                }
            }

            Console.WriteLine();

            if (result.TestLogs?.Summary.Sum((p) => p.Value.Count) > 0)
            {
                WriteTestResults(result.TestLogs);
            }

            return result.Success ? ProcessingResult.Success : ProcessingResult.Warning;
        }
    }

    private static TemporaryDirectory GetTestAdapter()
    {
        var loggerAssembly = typeof(BumperTestLogger).Assembly.Location;
        var copyFileName = Path.GetFileName(loggerAssembly);

        var directory = new TemporaryDirectory();

        try
        {
            File.Copy(loggerAssembly, Path.Combine(directory.Path, copyFileName), overwrite: true);
            return directory;
        }
        catch (Exception)
        {
            directory.Dispose();
            throw;
        }
    }

    private async Task<DotNetResult> RunTestsAsync(
        string sdkVersion,
        List<string> projects,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<DotNetResult>(projects.Count);

        var configuration = await configurationProvider.GetAsync(cancellationToken);

        foreach (var project in projects)
        {
            string name = ProjectHelpers.RelativeName(Options.ProjectPath, project);
            context.Status = StatusMessage($"Running tests for {name}...");

            var result = await RunTestsAsync(sdkVersion, project, configuration, cancellationToken);

            if (!result.Success)
            {
                return result;
            }

            results.Add(result);
        }

        if (results.Count is 1)
        {
            return results[0];
        }

        var overall = new DotNetResult(true, 0, string.Empty, string.Empty)
        {
            TestLogs = new(),
        };

        foreach (var result in results.Where((p) => p.TestLogs is not null))
        {
            overall.TestLogs.Outcomes = overall.TestLogs.Outcomes.Concat(result.TestLogs!.Outcomes).ToDictionary();
            overall.TestLogs.Summary = overall.TestLogs.Summary.Concat(result.TestLogs.Summary).ToDictionary();
        }

        return overall;
    }

    private async Task<DotNetResult> RunTestsAsync(
        string sdkVersion,
        string project,
        BumperConfiguration configuration,
        CancellationToken cancellationToken)
    {
        using var adapterDirectory = GetTestAdapter();
        using var logsDirectory = new TemporaryDirectory();

        var environmentVariables = new Dictionary<string, string?>()
        {
            [BumperTestLogger.LoggerDirectoryPathVariableName] = logsDirectory.Path,
        };

        MSBuildHelper.TryAddSdkProperties(environmentVariables, sdkVersion);

        TemporaryFile? propertiesOverrides = null;

        if (configuration.NoWarn is { Count: > 0 } noWarn)
        {
            propertiesOverrides = await GenerateDirectoryBuildPropsAsync(noWarn, cancellationToken);
            environmentVariables["DirectoryBuildPropsPath"] = propertiesOverrides.Path;
        }

        string[] arguments =
        [
            "test",
            "--configuration", "Release",
            "--logger", BumperTestLogger.ExtensionUri,
            "--nologo",
            "--test-adapter-path", adapterDirectory.Path,
            "--verbosity", Logger.GetMSBuildVerbosity(),
        ];

        DotNetResult result;

        try
        {
            // See https://learn.microsoft.com/dotnet/core/tools/dotnet-test
            result = await dotnet.RunWithLoggerAsync(
                project,
                arguments,
                environmentVariables,
                cancellationToken);
        }
        finally
        {
            propertiesOverrides?.Dispose();
        }

        result.TestLogs = await LogReader.GetTestLogsAsync(logsDirectory.Path, Logger, cancellationToken);

        return result;
    }

    private async Task<TemporaryFile> GenerateDirectoryBuildPropsAsync(
        HashSet<string> suppressWarnings,
        CancellationToken cancellationToken)
    {
        var artifactsPath = string.Empty;
        var import = string.Empty;
        var noWarn = string.Join(";", suppressWarnings);
        var useArtifactsOutput = "false";

        var existing = FileHelpers.FindFileInProject(Options.ProjectPath, WellKnownFileNames.DirectoryBuildProps);

        if (existing is not null)
        {
            import = $"<Import Project=\"{existing}\" />";

            // If the existing Directory.Build.props file has the UseArtifactsOutput property set to true
            // then it needs to be manually set in our override file, otherwise an NETSDK1200 error occurs.
            // See https://learn.microsoft.com/dotnet/core/tools/sdk-errors/.
            (artifactsPath, useArtifactsOutput) = await EvaluateArtifactsPropertiesAsync(existing, cancellationToken);
        }

        var project =
            $"""
             <Project>
               {import}
               <PropertyGroup>
                 <ArtifactsPath>{artifactsPath}</ArtifactsPath>
                 <NoWarn>$(NoWarn);{noWarn}</NoWarn>
                 <UseArtifactsOutput>{useArtifactsOutput}</UseArtifactsOutput>
               </PropertyGroup>
             </Project>
             """;

        var file = new TemporaryFile();

        try
        {
            await File.WriteAllTextAsync(file.Path, project, cancellationToken);
            return file;
        }
        catch (Exception)
        {
            file.Dispose();
            throw;
        }
    }

    private async Task<string> EvaluateMSBuildPropertyAsync(
        string projectPath,
        string propertyName,
        CancellationToken cancellationToken)
    {
        try
        {
            var getProperty = await dotnet.RunAsync(
                Options.ProjectPath,
                ["msbuild", projectPath, $"-getProperty:{propertyName}"],
                cancellationToken);

            if (getProperty.Success)
            {
                return getProperty.StandardOutput.Trim();
            }
        }
        catch (Exception ex)
        {
            Log.FailedToEvaluateProperty(Logger, propertyName, projectPath, ex);
        }

        return string.Empty;
    }

    private async Task<(string ArtifactsPath, string UseArtifactsOutput)> EvaluateArtifactsPropertiesAsync(
        string projectFile,
        CancellationToken cancellationToken)
    {
        var artifactsPath = string.Empty;
        var useArtifactsOutput = "false";

        var useArtifactsValue = await EvaluateMSBuildPropertyAsync(
            projectFile,
            "UseArtifactsOutput",
            cancellationToken);

        if (string.Equals(useArtifactsValue, bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            useArtifactsOutput = "true";

            artifactsPath = await EvaluateMSBuildPropertyAsync(
                projectFile,
                "ArtifactsPath",
                cancellationToken);

            if (string.IsNullOrWhiteSpace(artifactsPath))
            {
                artifactsPath = Path.Combine(Path.GetDirectoryName(projectFile)!, "artifacts");
            }
        }

        return (artifactsPath, useArtifactsOutput);
    }

    private void WriteBuildLogs(IDictionary<string, IDictionary<string, long>> summary)
    {
        var table = new Table
        {
            Title = new TableTitle("Errors and warnings"),
        };

        table.AddColumn("[bold]Type[/]");
        table.AddColumn("[bold]Id[/]");
        table.AddColumn("[bold]Count[/]");

        foreach ((var logType, var entriesById) in summary)
        {
            var color = logType switch
            {
                "Error" => Color.Red,
                "Warning" => Color.Yellow,
                _ => Color.Blue,
            };

            var typeEscaped = logType.EscapeMarkup();
            var type = new Markup($"[{color}]{typeEscaped}[/]");

            foreach ((var logId, var logCount) in entriesById)
            {
                string idMarkup = logId.EscapeMarkup();

                var id = new Markup(idMarkup);
                var count = new Markup(logCount.ToString(CultureInfo.CurrentCulture)).RightJustified();

                table.AddRow(type, id, count);
            }
        }

        Console.Write(table);
        Console.WriteLine();
    }

    private void WriteTestResults(BumperTestLog logs)
    {
        var table = new Table
        {
            Title = new TableTitle("dotnet test"),
        };

        const string Passed = "Passed";
        const string Failed = "Failed";
        const string Skipped = "Skipped";

        table.AddColumn("[bold]Container[/]");
        table.AddColumn($"[bold]{Passed}[/]");
        table.AddColumn($"[bold]{Failed}[/]");
        table.AddColumn($"[bold]{Skipped}[/]");

        foreach ((var container, var outcomes) in logs.Summary.Where((p) => p.Value.Count > 0).OrderBy((p) => p.Key))
        {
            var name = Container(container);
            var passed = Count(Passed, Color.Green, outcomes);
            var failed = Count(Failed, Color.Red, outcomes);
            var skipped = Count(Skipped, Color.Yellow, outcomes);

            table.AddRow(name, passed, failed, skipped);
        }

        Console.Write(table);
        Console.WriteLine();

        static Markup Container(string name) => new($"[{Color.Blue}]{name.EscapeMarkup()}[/]");

        static Markup Count(string key, Color color, IDictionary<string, long> outcomes)
        {
            if (!outcomes.TryGetValue(key, out long count))
            {
                count = 0;
            }

            return new Markup($"[{color}]{count.ToString(CultureInfo.CurrentCulture)}[/]");
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Failed to evaluate the value of the {PropertyName} MSBuild property from {ProjectFile}.")]
        public static partial void FailedToEvaluateProperty(ILogger logger, string propertyName, string projectFile, Exception exception);
    }
}
