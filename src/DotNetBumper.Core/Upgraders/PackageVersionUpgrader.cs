// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class PackageVersionUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<PackageVersionUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPackages(logger);

        console.WriteLine("Upgrading NuGet packages...");

        bool filesChanged = false;

        foreach (string project in FindProjects())
        {
            using (TryHideGlobalJson(project))
            {
                await RunDotNetCommandAsync(project, ["--info"], cancellationToken);
                await RunDotNetCommandAsync(project, ["--version"], cancellationToken);

                await TryRestoreNuGetPackagesAsync(project, cancellationToken);
                filesChanged |= await TryUpgradePackagesAsync(project, cancellationToken);
            }
        }

        return filesChanged;
    }

    private static Process StartDotNet(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(DotNetExe.FullPathOrDefault(), arguments)
        {
            EnvironmentVariables =
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for dotnet {arguments[0]}.");
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
    {
        string path = options.Value.ProjectPath;
        var searchOption = SearchOption.AllDirectories;

        List<string> projects =
        [
            ..Directory.GetFiles(path, "*.sln", searchOption),
        ];

        if (projects.Count == 0)
        {
            projects.AddRange(Directory.GetFiles(path, "*.csproj", searchOption));
            projects.AddRange(Directory.GetFiles(path, "*.fsproj", searchOption));
        }

        return projects
            .Select(Path.GetDirectoryName)
            .Cast<string>()
            .ToList();
    }

    private async Task TryRestoreNuGetPackagesAsync(string directory, CancellationToken cancellationToken)
    {
        if (await RunDotNetCommandAsync(directory, ["restore"], cancellationToken))
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

        if (options.Value.UpgradeType is UpgradeType.Preview)
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

        if (!await RunDotNetCommandAsync(directory, ["outdated", ..arguments], cancellationToken))
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

    private async Task<bool> RunDotNetCommandAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = StartDotNet(workingDirectory, arguments);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            await Log.LogCommandFailedAsync(logger, process);
            return false;
        }

        return true;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static async Task LogCommandFailedAsync(ILogger logger, Process process)
        {
            string command = process.StartInfo.ArgumentList[0];
            string output = await process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            string error = await process.StandardError.ReadToEndAsync(CancellationToken.None);

            Log.CommandFailed(logger, command, process.ExitCode);

            if (!string.IsNullOrEmpty(output))
            {
                Log.CommandFailedOutput(logger, command, output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                Log.CommandFailedError(logger, command, error);
            }
        }

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
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" failed with exit code {ExitCode}.")]
        public static partial void CommandFailed(ILogger logger, string command, int exitCode);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard output: {Output}",
            SkipEnabledCheck = true)]
        public static partial void CommandFailedOutput(ILogger logger, string command, string output);

        [LoggerMessage(
            EventId = 7,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard error: {Error}")]
        public static partial void CommandFailedError(ILogger logger, string command, string error);
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
