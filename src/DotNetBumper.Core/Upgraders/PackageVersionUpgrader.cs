// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    public override int Order => int.MaxValue - 1; // Packages need to be updated after the TFM so the packages relate to the update but before C# updates

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

        foreach (string project in FindProjects())
        {
            var name = RelativeName(project);

            context.Status = StatusMessage($"Updating {name}...");

            using (TryHideGlobalJson(project))
            using (TryDotNetToolManifest(project))
            {
                context.Status = StatusMessage($"Restore NuGet packages for {name}...");

                await TryRestoreNuGetPackagesAsync(project, cancellationToken);

                context.Status = StatusMessage($"Update NuGet packages for {name}...");

                result = result.Max(await TryUpgradePackagesAsync(project, cancellationToken));
            }
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update NuGet package versions for .NET {upgrade.Channel}");
        }

        return result;
    }

    private static HiddenFile? TryHideGlobalJson(string path)
    {
        var globalJson = FileHelpers.FindFileInProject(path, "global.json");

        if (globalJson != null)
        {
            return new HiddenFile(globalJson);
        }

        return null;
    }

    private static HiddenFile? TryDotNetToolManifest(string path)
    {
        var toolManifest = FileHelpers.FindFileInProject(path, Path.Join(".config", "dotnet-tools.json"));

        if (toolManifest != null)
        {
            return new HiddenFile(toolManifest);
        }

        return null;
    }

    private List<string> FindProjects()
        => ProjectHelpers.FindProjects(Options.ProjectPath, SearchOption.AllDirectories);

    private async Task TryRestoreNuGetPackagesAsync(string directory, CancellationToken cancellationToken)
    {
        var result = await dotnet.RunWithLoggerAsync(
            directory,
            ["restore", "--verbosity", "quiet"],
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

    private async Task<ProcessingResult> TryUpgradePackagesAsync(string directory, CancellationToken cancellationToken)
    {
        using var tempFile = new TemporaryFile();

        List<string> arguments =
        [
            "--output",
            tempFile.Path,
            "--output-format:json",
            "--upgrade",
        ];

        if (Options.UpgradeType is UpgradeType.Preview)
        {
            arguments.Add("--pre-release:Always");
        }

        var configuration = await configurationProvider.GetAsync(cancellationToken);

        foreach (string package in configuration.IncludeNuGetPackages)
        {
            arguments.Add("--include");
            arguments.Add(package);
        }

        foreach (string package in configuration.ExcludeNuGetPackages)
        {
            arguments.Add("--exclude");
            arguments.Add(package);
        }

        var environmentVariables = new Dictionary<string, string?>(1);

        if (configuration.NoWarn.Count > 0)
        {
            environmentVariables["NoWarn"] = string.Join(";", configuration.NoWarn);
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
    }

    private sealed class HiddenFile : IDisposable
    {
        private readonly string _original;
        private readonly string _temporary;

        public HiddenFile(string source)
        {
            _original = source;
            _temporary = $"{source}.{Guid.NewGuid().ToString()[0..8]}.tmp";
            File.Move(_original, _temporary);
        }

        public void Dispose()
            => File.Move(_temporary, _original, overwrite: true);
    }
}
