// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class PackageVersionUpgrader(
    IOptions<UpgradeOptions> options,
    ILogger<PackageVersionUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingPackages(logger);

        string tempFile = Path.GetTempFileName();

        List<string> arguments =
        [
            "outdated",
            "--output",
            tempFile,
            "--output-format:json",
            "--recursive",
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

        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            WorkingDirectory = options.Value.ProjectPath,
        };

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            // TODO log
            return false;
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
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
                var outdated = JsonDocument.Parse(json);
                var projects = outdated.RootElement.GetProperty("Projects");

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
