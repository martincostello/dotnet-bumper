// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class PackageVersionUpgrader(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IEnvironment environment,
    BumperConfigurationProvider configurationProvider,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<PackageVersionUpgrader> logger) : Upgrader(console, environment, options, logger)
{
    /// <summary>
    /// The minimum version of https://www.nuget.org/packages/dotnet-outdated-tool that supports the <c>--pre-release-label</c> option.
    /// </summary>
    private static readonly NuGetVersion MinimumVersionForPrereleaseLabel = new(4, 6, 1);

    /// <summary>
    /// The minimum version of https://www.nuget.org/packages/dotnet-outdated-tool that supports the <c>--maximum-version</c> option.
    /// </summary>
    private static readonly NuGetVersion MinimumVersionForMaximumVersion = new(4, 6, 5);

    protected override string Action => "Upgrading NuGet packages";

    protected override string InitialStatus => "Upgrade NuGet packages";

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPackages(logger);

        var result = ProcessingResult.None;

        context.Status = StatusMessage("Finding projects...");

        foreach (string projectFile in ProjectHelpers.FindProjectFiles(Options.ProjectPath))
        {
            var project = Path.GetDirectoryName(projectFile)!;
            var name = RelativeName(project);

            context.Status = StatusMessage($"Updating {name}...");

            using var globalJson = await PatchedGlobalJsonFile.TryPatchAsync(project, upgrade.SdkVersion, cancellationToken);
            var sdkVersion = globalJson?.SdkVersion ?? upgrade.SdkVersion.ToString();

            if (await HasWorkloadsAsync(projectFile, upgrade.SdkVersion, cancellationToken))
            {
                context.Status = StatusMessage($"Restore .NET workloads for {name}...");
                await TryRestoreWorkloadsAsync(project, sdkVersion, cancellationToken);
            }

            if (HasDotNetToolManifest(project))
            {
                context.Status = StatusMessage($"Restore .NET tools for {name}...");
                await TryRestoreToolsAsync(project, sdkVersion, cancellationToken);
            }

            context.Status = StatusMessage($"Restore NuGet packages for {name}...");

            await TryRestoreNuGetPackagesAsync(project, sdkVersion, cancellationToken);

            context.Status = StatusMessage($"Update NuGet packages for {name}...");

            result = result.Max(await TryUpgradePackagesAsync(project, upgrade.Channel, upgrade.SdkVersion, cancellationToken));
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update NuGet package versions for .NET {upgrade.Channel}");
        }

        return result;
    }

    private static bool HasDotNetToolManifest(string path)
        => FileHelpers.FindFileInProject(path, Path.Join(".config", WellKnownFileNames.ToolsManifest)) is not null;

    private async Task<NuGetVersion?> GetDotNetOutdatedVersionAsync(
        IDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        var result = await dotnet.RunAsync(
            Options.ProjectPath,
            ["outdated", "--version"],
            environmentVariables,
            cancellationToken: cancellationToken);

        if (result.Success)
        {
            using var reader = new StringReader(result.StandardOutput);

            string? line;

            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (NuGetVersion.TryParse(line, out var version))
                {
                    return version;
                }
            }
        }

        return null;
    }

    private async Task TryRestoreNuGetPackagesAsync(
        string directory,
        string sdkVersion,
        CancellationToken cancellationToken)
    {
        var environmentVariables = MSBuildHelper.GetSdkProperties(sdkVersion);
        environmentVariables[WellKnownEnvironmentVariables.MSBuildEnableWorkloadResolver] = "false";

        var result = await dotnet.RunWithLoggerAsync(
            directory,
            ["restore", "--verbosity", Logger.GetMSBuildVerbosity()],
            environmentVariables,
            cancellationToken);

        logContext.Add(result);

        if (result.Success)
        {
            Log.RestoredPackages(logger, directory);
        }
        else
        {
            Log.UnableToRestorePackages(logger, directory);
        }
    }

    private async Task TryRestoreToolsAsync(
        string directory,
        string sdkVersion,
        CancellationToken cancellationToken)
    {
        var environmentVariables = MSBuildHelper.GetSdkProperties(sdkVersion);

        var result = await dotnet.RunAsync(
            directory,
            ["tool", "restore", "--verbosity", Logger.GetMSBuildVerbosity()],
            environmentVariables,
            cancellationToken);

        logContext.Add(result);

        if (result.Success)
        {
            Log.RestoredTools(logger, directory);
        }
        else
        {
            Log.UnableToRestoreTools(logger, directory);
        }
    }

    private async Task TryRestoreWorkloadsAsync(
        string directory,
        string sdkVersion,
        CancellationToken cancellationToken)
    {
        var environmentVariables = MSBuildHelper.GetSdkProperties(sdkVersion);

        // This will require elevated permissions if anything needs to be installed
        var result = await dotnet.RunAsync(
            directory,
            ["workload", "restore", "--verbosity", Logger.GetMSBuildVerbosity()],
            environmentVariables,
            cancellationToken);

        logContext.Add(result);

        if (result.Success)
        {
            Log.RestoredWorkloads(logger, directory);
        }
        else
        {
            Log.UnableToRestoreWorkloads(logger, directory);
            Console.WriteWarningLine("Failed to restore .NET workloads. Elevated permissions may be required.");
        }
    }

    private async Task<ProcessingResult> TryUpgradePackagesAsync(
        string directory,
        Version channel,
        NuGetVersion sdkVersion,
        CancellationToken cancellationToken)
    {
        var environmentVariables = new Dictionary<string, string?>(5)
        {
            [WellKnownEnvironmentVariables.DotNetRollForward] = "Major",
            [WellKnownEnvironmentVariables.MSBuildEnableWorkloadResolver] = "false",
            [WellKnownEnvironmentVariables.MSBuildSdksPath] = null,
            [WellKnownEnvironmentVariables.NuGetAudit] = "false",
        };

        MSBuildHelper.TryAddSdkProperties(environmentVariables, sdkVersion.ToString());

        var outdatedVersion = await GetDotNetOutdatedVersionAsync(environmentVariables, cancellationToken);

        if (outdatedVersion is null)
        {
            Log.UnableToDetermineDotNetOutdatedToolVersion(Logger);
        }
        else
        {
            Log.DotNetOutdatedToolVersion(Logger, outdatedVersion);
        }

        using var tempFile = new TemporaryFile();

        List<string> arguments =
        [
            "--output",
            tempFile.Path,
            "--output-format:json",
            "--upgrade",
        ];

        // See https://github.com/dotnet-outdated/dotnet-outdated/pull/640
        if (outdatedVersion is null || outdatedVersion >= MinimumVersionForMaximumVersion)
        {
            arguments.AddRange(["--maximum-version", channel.ToString(2)]);
        }

        if (Options.UpgradeType.IsPrerelease())
        {
            arguments.Add("--pre-release:Always");

            // See https://github.com/dotnet-outdated/dotnet-outdated/pull/467
            if (sdkVersion.IsPrerelease && sdkVersion.ReleaseLabels.Count() > 2 &&
                (outdatedVersion is null || outdatedVersion >= MinimumVersionForPrereleaseLabel))
            {
                var label = string.Join('.', sdkVersion.ReleaseLabels.Take(2));
                arguments.AddRange(["--pre-release-label", label]);
            }
        }
        else
        {
            // Do not upgrade pre-release packages to newer pre-releases. For example,
            // if a project is using a .NET 6 preview package, it should be upgraded
            // to a .NET 8 version for an LTS upgrade, not to a .NET 9 preview version.
            arguments.Add("--pre-release:Never");
        }

        var configuration = await configurationProvider.GetAsync(cancellationToken);

        foreach (string package in configuration.IncludeNuGetPackages)
        {
            arguments.Add($"--include:{package}");
        }

        foreach (string package in configuration.ExcludeNuGetPackages)
        {
            arguments.Add($"--exclude:{package}");
        }

        if (configuration.NoWarn.Count > 0)
        {
            environmentVariables[WellKnownEnvironmentVariables.NoWarn] = string.Join(";", configuration.NoWarn);
        }

        var result = await dotnet.RunAsync(directory, ["outdated", .. arguments], environmentVariables, cancellationToken);

        logContext.Add(result);

        if (!result.Success)
        {
            string[] warnings =
            [
                $"Failed to upgrade NuGet packages for {RelativeName(directory)}.",
                $"dotnet outdated exited with code {result.ExitCode}.",
            ];

            Console.WriteLine();

            foreach (var warning in warnings)
            {
                Console.WriteWarningLine(warning);
                logContext.Warnings.Add(warning);
            }

            return ProcessingResult.Warning;
        }

        int updatedDependencies = 0;

        if (tempFile.Exists())
        {
            string json = await File.ReadAllTextAsync(tempFile.Path, cancellationToken);

            if (json.Length > 0)
            {
                var updates = JsonDocument.Parse(json, JsonHelpers.DocumentOptions);
                var projects = updates.RootElement.GetProperty("Projects");

                foreach (var project in projects.EnumerateArray())
                {
                    foreach (var tfm in project.GetProperty("TargetFrameworks").EnumerateArray())
                    {
                        updatedDependencies += tfm.GetProperty("Dependencies").EnumerateArray().Count();
                    }
                }
            }
        }

        Log.UpgradedPackages(logger, updatedDependencies);

        return updatedDependencies > 0 ? ProcessingResult.Success : ProcessingResult.None;
    }

    private async Task<bool> HasWorkloadsAsync(
        string projectFile,
        NuGetVersion sdkVersion,
        CancellationToken cancellationToken)
    {
        if (sdkVersion.Major < 8)
        {
            // "dotnet msbuild -getTargetResult" is not available before .NET 8
            // so just assume there are no workloads that need to be installed.
            return false;
        }

        // See https://github.com/dotnet/sdk/blob/051c52977e668544b58f60ff4d4ff84fe67d33f2/src/Cli/dotnet/commands/dotnet-workload/restore/WorkloadRestoreCommand.cs#L46-L81
        var projects = Path.GetExtension(projectFile) is (".sln" or ".slnx")
            ? ProjectHelpers.GetSolutionProjects(projectFile)
            : [projectFile];

        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [WellKnownEnvironmentVariables.MSBuildEnableWorkloadResolver] = "true",
            [WellKnownEnvironmentVariables.SkipResolvePackageAssets] = bool.TrueString,
        };

        MSBuildHelper.TryAddSdkProperties(environment, sdkVersion.ToString());

        foreach (var project in projects)
        {
            if (await HasWorkloadsAsync(project, environment, cancellationToken))
            {
                return true;
            }
        }

        return false;

        async Task<bool> HasWorkloadsAsync(
            string projectFile,
            Dictionary<string, string?> environment,
            CancellationToken cancellationToken)
        {
            const string WorkloadsTarget = "_GetRequiredWorkloads";

            var json = await EvaluateMSBuildTargetAsync(projectFile, WorkloadsTarget, environment, cancellationToken);

            if (string.IsNullOrWhiteSpace(json) || !JsonHelpers.TryLoadObjectFromString(json, out var document))
            {
                return false;
            }

            if (document.TryGetPropertyValue("TargetResults", out var results) &&
                results is JsonObject targetResults &&
                targetResults.TryGetPropertyValue(WorkloadsTarget, out var targetResult) &&
                targetResult is JsonObject result &&
                result.TryGetPropertyValue("Items", out var items) &&
                items is JsonArray array)
            {
                return array.Count > 0;
            }

            return false;
        }

        async Task<string?> EvaluateMSBuildTargetAsync(
            string projectPath,
            string targetName,
            Dictionary<string, string?> environment,
            CancellationToken cancellationToken)
        {
            try
            {
                var getTargetResult = await dotnet.RunAsync(
                    Options.ProjectPath,
                    ["msbuild", projectPath, $"-getTargetResult:{targetName}"],
                    environment,
                    cancellationToken);

                if (getTargetResult.Success)
                {
                    return getTargetResult.StandardOutput.Trim();
                }
            }
            catch (Exception ex)
            {
                Log.FailedToEvaluateTarget(Logger, targetName, projectPath, ex);
            }

            return null;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading NuGet package versions.")]
        public static partial void UpgradingPackages(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Upgraded {Count} NuGet package(s).")]
        public static partial void UpgradedPackages(ILogger logger, int count);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Restored NuGet packages for {Directory}.")]
        public static partial void RestoredPackages(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Unable to restore NuGet packages for {Directory}.")]
        public static partial void UnableToRestorePackages(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Restored .NET tools for {Directory}.")]
        public static partial void RestoredTools(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Unable to restore .NET tools for {Directory}.")]
        public static partial void UnableToRestoreTools(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Debug,
            Message = "Restored .NET workloads for {Directory}.")]
        public static partial void RestoredWorkloads(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 8,
            Level = LogLevel.Warning,
            Message = "Unable to restore .NET workloads for {Directory}.")]
        public static partial void UnableToRestoreWorkloads(ILogger logger, string directory);

        [LoggerMessage(
            EventId = 9,
            Level = LogLevel.Debug,
            Message = "Failed to evaluate the result of the {TargetName} MSBuild target from {ProjectFile}.")]
        public static partial void FailedToEvaluateTarget(ILogger logger, string targetName, string projectFile, Exception exception);

        [LoggerMessage(
            EventId = 10,
            Level = LogLevel.Debug,
            Message = "Installed version of dotnet-outdated-tool is {Version}.")]
        public static partial void DotNetOutdatedToolVersion(ILogger logger, NuGetVersion version);

        [LoggerMessage(
            EventId = 11,
            Level = LogLevel.Warning,
            Message = "Unable to determine the installed version of dotnet-outdated-tool.")]
        public static partial void UnableToDetermineDotNetOutdatedToolVersion(ILogger logger);
    }
}
