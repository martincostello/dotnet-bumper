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

            using (await TryPatchGlobalJsonAsync(project, upgrade.SdkVersion.IsPrerelease, cancellationToken))
            {
                if (HasDotNetToolManifest(project))
                {
                    context.Status = StatusMessage($"Restore .NET tools for {name}...");
                    await TryRestoreToolsAsync(project, cancellationToken);
                }

                context.Status = StatusMessage($"Restore NuGet packages for {name}...");

                await TryRestoreNuGetPackagesAsync(project, cancellationToken);

                context.Status = StatusMessage($"Update NuGet packages for {name}...");

                result = result.Max(await TryUpgradePackagesAsync(project, upgrade.SdkVersion, cancellationToken));
            }
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update NuGet package versions for .NET {upgrade.Channel}");
        }

        return result;
    }

    private static async Task<PatchedGlobalJsonFile?> TryPatchGlobalJsonAsync(
        string path,
        bool isPrerelease,
        CancellationToken cancellationToken)
    {
        var globalJson = FileHelpers.FindFileInProject(path, WellKnownFileNames.GlobalJson);

        if (globalJson != null)
        {
            var patched = new PatchedGlobalJsonFile(globalJson);

            try
            {
                await patched.TryRemoveSdkVersionAsync(isPrerelease, cancellationToken);
                return patched;
            }
            catch (Exception)
            {
                patched.Dispose();
                throw;
            }
        }

        return null;
    }

    private static bool HasDotNetToolManifest(string path)
        => FileHelpers.FindFileInProject(path, Path.Join(".config", WellKnownFileNames.ToolsManifest)) is not null;

    private List<string> FindProjects()
        => ProjectHelpers.FindProjects(Options.ProjectPath, SearchOption.AllDirectories);

    private string GetVerbosity()
        => Logger.IsEnabled(LogLevel.Debug) ? "detailed" : "quiet";

    private async Task TryRestoreNuGetPackagesAsync(string directory, CancellationToken cancellationToken)
    {
        var result = await dotnet.RunWithLoggerAsync(
            directory,
            ["restore", "--verbosity", GetVerbosity()],
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

    private async Task TryRestoreToolsAsync(string directory, CancellationToken cancellationToken)
    {
        var result = await dotnet.RunAsync(
            directory,
            ["tool", "restore", "--verbosity", GetVerbosity()],
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

    private async Task<ProcessingResult> TryUpgradePackagesAsync(
        string directory,
        NuGetVersion sdkVersion,
        CancellationToken cancellationToken)
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

            // Requires .NET Outdated v4.6.1+.
            // See https://github.com/dotnet-outdated/dotnet-outdated/pull/467.
            if (sdkVersion.IsPrerelease && sdkVersion.ReleaseLabels.Count() > 2)
            {
                var label = string.Join('.', sdkVersion.ReleaseLabels.Take(2));

                arguments.Add("--pre-release-label");
                arguments.Add(label);
            }
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
    }

    private sealed class PatchedGlobalJsonFile : IDisposable
    {
        private readonly string _filePath;
        private readonly string _backupPath;

        public PatchedGlobalJsonFile(string source)
        {
            _filePath = source;
            _backupPath = $"{source}.{Guid.NewGuid().ToString()[0..8]}.tmp";
            File.Copy(_filePath, _backupPath, overwrite: true);
        }

        public void Dispose()
            => File.Move(_backupPath, _filePath, overwrite: true);

        public async Task TryRemoveSdkVersionAsync(bool isPrerelease, CancellationToken cancellationToken)
        {
            if (!JsonHelpers.TryLoadObject(_filePath, out var globalJson))
            {
                return;
            }

            const string AllowPrereleaseProperty = "allowPrerelease";
            const string SdkProperty = "sdk";
            const string VersionProperty = "version";

            // Drop the version from the SDK property in the global.json file
            // but keep any other content, such as versions for MSBuild SDKs.
            if (globalJson.TryGetPropertyValue(SdkProperty, out var property) &&
                property?.GetValueKind() is JsonValueKind.Object)
            {
                var sdk = property.AsObject();

                var edited = false;

                if (sdk.TryGetPropertyValue(VersionProperty, out var version) &&
                    version?.GetValueKind() is JsonValueKind.String)
                {
                    edited = sdk.Remove(VersionProperty);
                }

                if (isPrerelease &&
                    (!sdk.TryGetPropertyValue(AllowPrereleaseProperty, out var allowPrerelease) ||
                     allowPrerelease?.GetValueKind() is not JsonValueKind.True))
                {
                    sdk.Add(new(AllowPrereleaseProperty, JsonValue.Create(true)));
                    edited = true;
                }

                if (edited)
                {
                    await globalJson.SaveAsync(_filePath, cancellationToken);
                }
            }
        }
    }
}
