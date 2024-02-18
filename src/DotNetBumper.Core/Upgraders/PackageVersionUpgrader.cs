// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
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

        await TryRestoreNuGetPackagesAsync(cancellationToken);

        return await TryUpgradePackagesAsync(cancellationToken);
    }

    private async Task TryRestoreNuGetPackagesAsync(CancellationToken cancellationToken)
    {
        if (!await RunDotNetCommandAsync(["restore", options.Value.ProjectPath], cancellationToken))
        {
            Log.UnableToRestore(logger);
        }
    }

    private async Task<bool> TryUpgradePackagesAsync(CancellationToken cancellationToken)
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

        if (!await RunDotNetCommandAsync(["outdated", .. arguments], cancellationToken))
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

    private async Task<bool> RunDotNetCommandAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = StartDotNet(arguments);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            await Log.LogCommandFailedAsync(logger, process);
            return false;
        }

        return true;
    }

    private Process StartDotNet(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            EnvironmentVariables =
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = options.Value.ProjectPath,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for dotnet {arguments[0]}.");
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
            Level = LogLevel.Warning,
            Message = "Unable to restore NuGet packages.")]
        public static partial void UnableToRestore(ILogger logger);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" failed with exit code {ExitCode}.")]
        public static partial void CommandFailed(ILogger logger, string command, int exitCode);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard output: {Output}",
            SkipEnabledCheck = true)]
        public static partial void CommandFailedOutput(ILogger logger, string command, string output);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard error: {Error}")]
        public static partial void CommandFailedError(ILogger logger, string command, string error);
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
