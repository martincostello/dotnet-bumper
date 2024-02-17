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

        using var restore = StartDotNet(["restore", options.Value.ProjectPath]);
        await restore!.WaitForExitAsync(cancellationToken);

        if (restore.ExitCode != 0)
        {
            // TODO log
            return false;
        }

        string tempFile = Path.GetTempFileName();

        List<string> arguments =
        [
            "outdated",
            "--output",
            tempFile,
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

        using var outdated = StartDotNet(arguments);

        if (outdated is null)
        {
            // TODO log
            return false;
        }

        await outdated.WaitForExitAsync(cancellationToken);

        if (outdated.ExitCode != 0)
        {
            // TODO log
            return false;
        }

        int updatedDependencies = 0;

        if (File.Exists(tempFile))
        {
            string json = await File.ReadAllTextAsync(tempFile, cancellationToken);

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

        try
        {
            File.Delete(tempFile);
        }
        catch (Exception)
        {
            // Ignore
        }

        return updatedDependencies > 0;
    }

    private Process? StartDotNet(IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            EnvironmentVariables =
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
            },
            RedirectStandardOutput = true,
            WorkingDirectory = options.Value.ProjectPath,
        };

        return Process.Start(startInfo);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Upgrading NuGet package versions.")]
        public static partial void UpgradingPackages(ILogger logger);
    }
}
