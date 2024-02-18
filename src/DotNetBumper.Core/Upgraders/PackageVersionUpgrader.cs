﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class PackageVersionUpgrader(
    DotNetProcess dotnet,
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<PackageVersionUpgrader> logger) : Upgrader(console, options, logger)
{
    protected override string Action => "Upgrading NuGet packages";

    protected override string InitialStatus => "Upgrade NuGet packages";

    protected override async Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPackages(logger);

        bool filesChanged = false;

        context.Status = StatusMessage("Finding projects...");

        foreach (string project in FindProjects())
        {
            var name = RelativeName(project);

            context.Status = StatusMessage($"Updating {name}...");

            using (TryHideGlobalJson(project))
            {
                context.Status = StatusMessage($"Restore NuGet packages for {name}...");

                await TryRestoreNuGetPackagesAsync(project, cancellationToken);

                context.Status = StatusMessage($"Update NuGet packages for {name}...");

                filesChanged |= await TryUpgradePackagesAsync(project, cancellationToken);
            }
        }

        return filesChanged;
    }

    private static HiddenFile? TryHideGlobalJson(string path)
    {
        var directory = new DirectoryInfo(path);

        do
        {
            var globalJson = Directory.EnumerateFiles(directory.FullName, "global.json").FirstOrDefault();

            if (globalJson != null)
            {
                return new HiddenFile(Path.GetFullPath(Path.Combine(directory.FullName, globalJson)));
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
        (bool success, _) = await dotnet.RunAsync(
            directory,
            ["restore", "--verbosity", "quiet"],
            cancellationToken);

        if (success)
        {
            Log.RestoredPackages(logger, directory);
        }
        else
        {
            Log.UnableToRestorePackages(logger, directory);
        }
    }

    private async Task<bool> TryUpgradePackagesAsync(string directory, CancellationToken cancellationToken)
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

        (bool success, _) = await dotnet.RunAsync(directory, ["outdated", ..arguments], cancellationToken);

        if (!success)
        {
            return false;
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

        return updatedDependencies > 0;
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
