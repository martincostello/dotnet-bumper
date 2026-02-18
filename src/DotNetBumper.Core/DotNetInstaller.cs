// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that installs the .NET SDK. This class cannot be inherited.
/// </summary>
/// <param name="httpClient">The <see cref="HttpClient"/> to use.</param>
/// <param name="logger">The <see cref="ILogger"/> to use.</param>
internal sealed partial class DotNetInstaller(HttpClient httpClient, ILogger logger)
{
    private static DirectoryInfo? _temporaryDirectory;

    public async Task TryInstallAsync(NuGetVersion sdkVersion, CancellationToken cancellationToken)
    {
        var version = sdkVersion.ToString();
        var installationPath = EnsureInstallationPath();

        if (IsDotNetSdkInstalled(installationPath, version))
        {
            SetDotNetRoot(installationPath);
            Log.SdkAlreadyInstalled(logger, version);
            return;
        }

        var scriptPath = await DownloadInstallScriptAsync(installationPath, cancellationToken);

        var command = OperatingSystem.IsWindows() ? "powershell" : scriptPath;

        string[] scriptArgs =
            OperatingSystem.IsWindows() ?
            ["-File", scriptPath, "-Version", version, "-InstallDir", installationPath, "-SkipNonVersionedFiles"] :
            ["--version", version, "--install-dir", installationPath, "--skip-non-versioned-files"];

        var startInfo = new ProcessStartInfo(command, scriptArgs)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        Log.InstallingSdk(logger, version);

        var result = await ProcessHelper.RunAsync(startInfo, cancellationToken);

        if (result.Success)
        {
            SetDotNetRoot(installationPath);
            Log.InstalledSdk(logger, version);
        }
        else
        {
            Log.InstallationFailed(logger, Path.GetFileName(scriptPath), result);
        }

        static void SetDotNetRoot(string installationPath) =>
            Environment.SetEnvironmentVariable(
                WellKnownEnvironmentVariables.DotNetRoot,
                installationPath,
                EnvironmentVariableTarget.Process);
    }

    private static string EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    private static string EnsureTemporaryDirectory()
    {
        _temporaryDirectory ??= Directory.CreateTempSubdirectory("dotnet-bumper-");
        return _temporaryDirectory.FullName;
    }

    private static string EnsureInstallationPath()
        => EnsureDirectory(Path.Join(EnsureTemporaryDirectory(), ".dotnet"));

    private static bool IsDotNetSdkInstalled(string installationPath, string version)
    {
        var sdkPath = Path.Join(installationPath, "sdk", version);
        return Directory.Exists(sdkPath);
    }

    private async Task<string> DownloadInstallScriptAsync(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var isWindows = OperatingSystem.IsWindows();
        var extension = isWindows ? "ps1" : "sh";
        var fileName = $"dotnet-install.{extension}";

        var requestUri = new Uri($"https://dot.net/v1/{fileName}");

        var scriptBytes = await httpClient.GetByteArrayAsync(requestUri, cancellationToken);

        var scriptPath = Path.Join(directoryPath, fileName);

        await File.WriteAllBytesAsync(scriptPath, scriptBytes, cancellationToken);

        if (!isWindows)
        {
            // Make the script executable
            using var chmod = Process.Start("chmod", ["+x", scriptPath]);
            if (chmod is not null)
            {
                await chmod.WaitForExitAsync(cancellationToken);
            }
        }

        return scriptPath;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Information,
            Message = "Version {Version} of the .NET SDK is already installed.")]
        public static partial void SdkAlreadyInstalled(ILogger logger, string version);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Information,
            Message = "Installing version {Version} of the .NET SDK.")]
        public static partial void InstallingSdk(ILogger logger, string version);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Information,
            Message = "Installed version {Version} of the .NET SDK successfully.")]
        public static partial void InstalledSdk(ILogger logger, string version);

        public static void InstallationFailed(
            ILogger logger,
            string fileName,
            ProcessResult process)
        {
            InstallFailed(logger, fileName, process.ExitCode);

            if (!string.IsNullOrEmpty(process.StandardOutput))
            {
                InstallFailedOutput(logger, fileName, process.StandardOutput);
            }

            if (!string.IsNullOrEmpty(process.StandardError))
            {
                InstallFailedError(logger, fileName, process.StandardError);
            }
        }

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "{InstallerScript} failed with exit code {ExitCode}.")]
        public static partial void InstallFailed(ILogger logger, string installerScript, int exitCode);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Warning,
            Message = "{InstallerScript} standard output: {Output}")]
        public static partial void InstallFailedOutput(ILogger logger, string installerScript, string output);

        [LoggerMessage(
            EventId = 6,
            Level = LogLevel.Error,
            Message = "{InstallerScript} standard error: {Error}")]
        public static partial void InstallFailedError(ILogger logger, string installerScript, string error);
    }
}
