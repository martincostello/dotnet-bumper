// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class PackageVersionUpgrader(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<PackageVersionUpgrader> logger) : Upgrader(console, options, logger)
{
    public override int Order => int.MaxValue; // Packages need to be updated after the TFM so the packages relate to the update

    protected override string Action => "Upgrading NuGet packages";

    protected override string InitialStatus => "Upgrade NuGet packages";

    protected override async Task<UpgradeResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPackages(logger);

        UpgradeResult result = UpgradeResult.None;

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

        return result;
    }

    private static HiddenFile? TryHideGlobalJson(string path)
    {
        var directory = new DirectoryInfo(path);

        do
        {
            var globalJson = Directory.EnumerateFiles(directory.FullName, "global.json").FirstOrDefault();

            if (globalJson != null)
            {
                return new HiddenFile(globalJson);
            }

            directory = directory.Parent;
        }
        while (directory is not null);

        return null;
    }

    private static HiddenFile? TryDotNetToolManifest(string path)
    {
        var directory = new DirectoryInfo(path);

        do
        {
            var configPath = Path.Combine(directory.FullName, ".config");

            if (Directory.Exists(configPath))
            {
                var toolManifest = Directory.EnumerateFiles(configPath, "dotnet-tools.json").FirstOrDefault();

                if (toolManifest != null)
                {
                    return new HiddenFile(toolManifest);
                }
            }

            directory = directory.Parent;
        }
        while (directory is not null);

        return null;
    }

    private List<string> FindProjects()
        => ProjectHelpers.FindProjects(Options.ProjectPath, SearchOption.AllDirectories);

    private async Task TryRestoreNuGetPackagesAsync(string directory, CancellationToken cancellationToken)
    {
        var result = await dotnet.RunAsync(
            directory,
            ["restore", "--verbosity", "quiet"],
            cancellationToken);

        if (result.Success)
        {
            Log.RestoredPackages(logger, directory);
        }
        else
        {
            Log.UnableToRestorePackages(logger, directory);
        }
    }

    private async Task<UpgradeResult> TryUpgradePackagesAsync(string directory, CancellationToken cancellationToken)
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

        string[] packages =
        [
            "Microsoft.AspNetCore.",
            "Microsoft.EntityFrameworkCore.",
            "Microsoft.Extensions.",
            "System.Text.Json",
        ];

        foreach (string package in packages)
        {
            arguments.Add($"--include");
            arguments.Add(package);
        }

        var result = await dotnet.RunAsync(directory, ["outdated", ..arguments], cancellationToken);

        if (!result.Success)
        {
            Console.WriteLine();
            Console.WriteWarningLine($"Failed to upgrade NuGet packages for {RelativeName(directory)}.");
            Console.WriteWarningLine($"dotnet outdated exited with code {result.ExitCode}.");

            return UpgradeResult.Warning;
        }

        int updatedDependencies = 0;

        if (tempFile.Exists())
        {
            string json = await File.ReadAllTextAsync(tempFile.Path, cancellationToken);

            if (json.Length > 0)
            {
                var updates = JsonDocument.Parse(json);
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

        return updatedDependencies > 0 ? UpgradeResult.Success : UpgradeResult.None;
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

    private sealed class TemporaryFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        public bool Exists() => File.Exists(Path);
    }
}
