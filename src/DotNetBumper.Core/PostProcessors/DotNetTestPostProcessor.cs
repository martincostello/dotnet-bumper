// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
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
            var result = await RunTestsAsync(
                projects,
                upgrade.SdkVersion,
                context,
                cancellationToken);

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

                if (Logger.IsEnabled(LogLevel.Debug) && result.TestLogs?.Outcomes is { } outcomes)
                {
                    foreach ((string container, var tests) in outcomes)
                    {
                        foreach (var test in tests.Where((p) => p.Outcome is "Failed"))
                        {
                            Log.TestFailed(Logger, container, test.Id, test.ErrorMessage);
                        }
                    }
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

    private static bool UsesMicrosoftTestingPlatformRunner(string projectDirectory)
    {
        var globalJson = FileHelpers.FindFileInProject(projectDirectory, WellKnownFileNames.GlobalJson);

        if (globalJson is null)
        {
            return false;
        }

        try
        {
            if (JsonHelpers.TryLoadObject(globalJson, out var root) &&
                root.TryGetPropertyValue("test", out var test) &&
                test is JsonObject testObject &&
                testObject.TryGetPropertyValue("runner", out var runner) &&
                runner is JsonValue value &&
                value.GetValueKind() is JsonValueKind.String &&
                string.Equals(value.GetValue<string>(), "Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (Exception)
        {
            // Ignore malformed global.json file
        }

        return false;
    }

    private static List<string> ResolveProjectFiles(string projectDirectory)
    {
        var projectFiles = new List<string>();

        foreach (var file in ProjectHelpers.FindProjectFiles(projectDirectory, SearchOption.TopDirectoryOnly))
        {
            if (Path.GetExtension(file) is ".sln" or ".slnx")
            {
                projectFiles.AddRange(ProjectHelpers.GetSolutionProjects(file));
            }
            else
            {
                projectFiles.Add(file);
            }
        }

        return projectFiles;
    }

    private async Task<DotNetResult> RunTestsAsync(
        List<string> projects,
        NuGetVersion sdkVersion,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<DotNetResult>(projects.Count);

        var configuration = await configurationProvider.GetAsync(cancellationToken);

        foreach (var project in projects)
        {
            string name = ProjectHelpers.RelativeName(Options.ProjectPath, project);
            context.Status = StatusMessage($"Running tests for {name}...");

            var result = await RunTestsAsync(project, sdkVersion, configuration, cancellationToken);

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
        string project,
        NuGetVersion sdkVersion,
        BumperConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var environmentVariables = new Dictionary<string, string?>()
        {
            [WellKnownEnvironmentVariables.NuGetAudit] = "false",
        };

        if (sdkVersion.IsPrerelease)
        {
            // Enable roll-forward for pre-release versions of the .NET SDK.
            // Otherwise this can create issues with using .NET local tools,
            // for example using Microsoft.Extensions.ApiDescription.Server
            // to generate an OpenAPI document as part of building an app.
            environmentVariables[WellKnownEnvironmentVariables.DotNetRollForward] = "Major";
        }

        MSBuildHelper.TryAddSdkProperties(environmentVariables, sdkVersion.ToString());

        TemporaryFile? propertiesOverrides = null;

        if (configuration.NoWarn is { Count: > 0 } noWarn)
        {
            propertiesOverrides = await GenerateDirectoryBuildPropsAsync(project, sdkVersion, noWarn, cancellationToken);
            environmentVariables[WellKnownEnvironmentVariables.DirectoryBuildPropertiesPath] = propertiesOverrides.Path;
        }

        try
        {
            // The custom VSTest logger cannot be used with Microsoft Testing Platform (MTP),
            // so when MTP is in use the tests are run differently and the results captured from
            // a TRX report instead (if the relevant extension is available to the project).
            var platform = await DetectTestPlatformAsync(project, cancellationToken);

            return platform is TestPlatform.MicrosoftTestingPlatform
                ? await RunTestsWithTestingPlatformAsync(project, environmentVariables, cancellationToken)
                : await RunTestsWithVSTestAsync(project, environmentVariables, cancellationToken);
        }
        finally
        {
            propertiesOverrides?.Dispose();
        }
    }

    private async Task<DotNetResult> RunTestsWithVSTestAsync(
        string project,
        Dictionary<string, string?> environmentVariables,
        CancellationToken cancellationToken)
    {
        using var adapterDirectory = GetTestAdapter();
        using var logsDirectory = new TemporaryDirectory();

        environmentVariables[BumperTestLogger.LoggerDirectoryPathVariableName] = logsDirectory.Path;

        string[] arguments =
        [
            "test",
            "--configuration",
            "Release",
            "--logger",
            BumperTestLogger.ExtensionUri,
            "--nologo",
            "--test-adapter-path",
            adapterDirectory.Path,
            "--verbosity",
            Logger.GetMSBuildVerbosity(),
        ];

        // See https://learn.microsoft.com/dotnet/core/tools/dotnet-test
        var result = await dotnet.RunWithLoggerAsync(
            project,
            arguments,
            environmentVariables,
            cancellationToken);

        result.TestLogs = await LogReader.GetTestLogsAsync(logsDirectory.Path, Logger, cancellationToken);

        return result;
    }

    private async Task<DotNetResult> RunTestsWithTestingPlatformAsync(
        string project,
        Dictionary<string, string?> environmentVariables,
        CancellationToken cancellationToken)
    {
        // The custom MSBuild logger used to capture build errors and warnings cannot be passed to
        // "dotnet test" when Microsoft Testing Platform is in use, so build the project separately
        // first (which the logger does support) and then run the already-built tests.
        // See https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test.
        string[] buildArguments =
        [
            "build",
            "--configuration",
            "Release",
            "--verbosity",
            Logger.GetMSBuildVerbosity(),
        ];

        var buildResult = await dotnet.RunWithLoggerAsync(
            project,
            buildArguments,
            environmentVariables,
            cancellationToken);

        if (!buildResult.Success)
        {
            return buildResult;
        }

        // Unlike VSTest, "dotnet test" with Microsoft Testing Platform fails if it is run for a
        // project that is not a test project (such as an application project), so there is nothing
        // more to do in that case as the test projects that reference it have now been built too.
        // This is determined after the build so that the relevant MSBuild properties are available.
        if (!await IsTestProjectAsync(project, cancellationToken))
        {
            return buildResult;
        }

        bool supportsTrx = await SupportsTrxReportAsync(project, cancellationToken);

        using var resultsDirectory = new TemporaryDirectory();

        List<string> testArguments =
        [
            "test",
            "--no-build",
            "--configuration",
            "Release",
        ];

        if (supportsTrx)
        {
            // The results directory is an argument to "dotnet test" itself, whereas arguments after
            // "--" are forwarded to the test application, which produces a TRX report in that
            // directory which the test results are then read from.
            testArguments.Add("--results-directory");
            testArguments.Add(resultsDirectory.Path);
            testArguments.Add("--");
            testArguments.Add("--report-trx");
        }

        var result = await dotnet.RunAsync(
            project,
            testArguments,
            environmentVariables,
            cancellationToken);

        result.BuildLogs = buildResult.BuildLogs;

        if (supportsTrx)
        {
            result.TestLogs = await LogReader.GetTestLogsFromTrxAsync(resultsDirectory.Path, Logger, cancellationToken);
        }

        return result;
    }

    private async Task<TestPlatform> DetectTestPlatformAsync(
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        if (UsesMicrosoftTestingPlatformRunner(projectDirectory))
        {
            return TestPlatform.MicrosoftTestingPlatform;
        }

        foreach (var projectFile in ResolveProjectFiles(projectDirectory))
        {
            var value = await EvaluateMSBuildPropertyAsync(
                projectFile,
                "TestingPlatformDotnetTestSupport",
                cancellationToken);

            if (string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return TestPlatform.MicrosoftTestingPlatform;
            }
        }

        return TestPlatform.VSTest;
    }

    private async Task<bool> IsTestProjectAsync(
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        foreach (var projectFile in ResolveProjectFiles(projectDirectory))
        {
            var isTestProject = await EvaluateMSBuildPropertyAsync(projectFile, "IsTestProject", cancellationToken);
            var isTestingPlatformApplication = await EvaluateMSBuildPropertyAsync(projectFile, "IsTestingPlatformApplication", cancellationToken);

            if (string.Equals(isTestProject, bool.TrueString, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(isTestingPlatformApplication, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> SupportsTrxReportAsync(
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        const string TrxReportPackage = "Microsoft.Testing.Extensions.TrxReport";

        foreach (var projectFile in ResolveProjectFiles(projectDirectory))
        {
            var packages = await EvaluateMSBuildItemsAsync(projectFile, "PackageReference", cancellationToken);

            if (packages.Any((p) => string.Equals(p, TrxReportPackage, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> EvaluateMSBuildItemsAsync(
        string projectFile,
        string itemType,
        CancellationToken cancellationToken)
    {
        try
        {
            var getItem = await dotnet.RunAsync(
                Options.ProjectPath,
                ["msbuild", projectFile, $"-getItem:{itemType}"],
                cancellationToken);

            if (getItem.Success && !string.IsNullOrWhiteSpace(getItem.StandardOutput))
            {
                using var document = JsonDocument.Parse(getItem.StandardOutput);

                if (document.RootElement.TryGetProperty("Items", out var items) &&
                    items.TryGetProperty(itemType, out var values) &&
                    values.ValueKind is JsonValueKind.Array)
                {
                    var identities = new List<string>(values.GetArrayLength());

                    foreach (var value in values.EnumerateArray())
                    {
                        if (value.TryGetProperty("Identity", out var identity) &&
                            identity.ValueKind is JsonValueKind.String &&
                            identity.GetString() is { Length: > 0 } id)
                        {
                            identities.Add(id);
                        }
                    }

                    return identities;
                }
            }
        }
        catch (Exception ex)
        {
            Log.FailedToEvaluateItems(Logger, itemType, projectFile, ex);
        }

        return [];
    }

    private async Task<TemporaryFile> GenerateDirectoryBuildPropsAsync(
        string projectPath,
        NuGetVersion sdkVersion,
        HashSet<string> suppressWarnings,
        CancellationToken cancellationToken)
    {
        var artifactsPath = string.Empty;
        var import = string.Empty;
        var noWarn = string.Join(";", suppressWarnings);
        var useArtifactsOutput = "false";

        var existing = FileHelpers.FindFileInProject(projectPath, WellKnownFileNames.DirectoryBuildProps);

        if (existing is not null)
        {
            import = $"<Import Project=\"{existing}\" />";

            // If the existing Directory.Build.props file has the UseArtifactsOutput property set to true
            // then it needs to be manually set in our override file, otherwise an NETSDK1200 error occurs.
            // See https://learn.microsoft.com/dotnet/core/tools/sdk-errors/.
            (artifactsPath, useArtifactsOutput) = await EvaluateArtifactsPropertiesAsync(sdkVersion, existing, cancellationToken);
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
        NuGetVersion sdkVersion,
        string projectFile,
        CancellationToken cancellationToken)
    {
        var artifactsPath = string.Empty;
        var useArtifactsOutput = "false";

        if (sdkVersion.Major < 8)
        {
            // "dotnet msbuild -getProperty" is not available before .NET 8
            return (artifactsPath, useArtifactsOutput);
        }

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

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Test {Container}.{Id} failed: {ErrorMessage}")]
        public static partial void TestFailed(ILogger logger, string container, string? id, string? errorMessage);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Failed to evaluate the {ItemType} MSBuild items from {ProjectFile}.")]
        public static partial void FailedToEvaluateItems(ILogger logger, string itemType, string projectFile, Exception exception);
    }
}
