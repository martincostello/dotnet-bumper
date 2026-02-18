// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// Runs a .NET process.
/// </summary>
/// <param name="logger">The <see cref="ILogger{DotNetProcess}"/> to use.</param>
public sealed partial class DotNetProcess(ILogger<DotNetProcess> logger)
{
    /// <summary>
    /// Tries to find the path of the .NET installation on the current machine.
    /// </summary>
    /// <returns>
    /// The actual path of the .NET installation on the current machine, if
    /// found; otherwise the default location for the current operating system.
    /// </returns>
    public static string TryFindDotNetInstallation()
    {
        // Adapted from https://github.com/natemcmaster/CommandLineUtils/blob/210871add72e8ad22661194c6f630fc1ecee140f/src/CommandLineUtils/Utilities/DotNetExe.cs#L1
        var dotnetRoot = Environment.GetEnvironmentVariable(WellKnownEnvironmentVariables.DotNetRoot);

        if (string.IsNullOrEmpty(dotnetRoot))
        {
            // See https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables
            string[] candidates = OperatingSystem.IsWindows() ?
            [
                Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
            ]
            :
            [
                "/usr/local/share/dotnet",
                "/usr/share/dotnet",
                "/usr/lib/dotnet",
            ];

            string? root = null;

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    root = candidate;
                    break;
                }
            }

            dotnetRoot = root ?? candidates[0];
        }

        return dotnetRoot;
    }

    /// <summary>
    /// Runs the specified dotnet command.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="arguments">The arguments to the command.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{DotNetResult}"/> representing the asynchronous operation to run the command.
    /// </returns>
    public async Task<DotNetResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return await RunAsync(workingDirectory, null, arguments, null, cancellationToken);
    }

    /// <summary>
    /// Runs the specified dotnet command.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="arguments">The arguments to the command.</param>
    /// <param name="environmentVariables">The environment variables to set for the process, if any.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{DotNetResult}"/> representing the asynchronous operation to run the command.
    /// </returns>
    public async Task<DotNetResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        return await RunAsync(workingDirectory, null, arguments, environmentVariables, cancellationToken);
    }

    /// <summary>
    /// Runs the specified dotnet command with a custom MSBuild logger.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="arguments">The arguments to the command.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{DotNetResult}"/> representing the asynchronous operation to run the command.
    /// </returns>
    public async Task<DotNetResult> RunWithLoggerAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return await RunWithLoggerAsync(workingDirectory, arguments, null, cancellationToken);
    }

    /// <summary>
    /// Runs the specified dotnet command with a custom MSBuild logger.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="arguments">The arguments to the command.</param>
    /// <param name="environmentVariables">The environment variables to set for the process, if any.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{DotNetResult}"/> representing the asynchronous operation to run the command.
    /// </returns>
    public async Task<DotNetResult> RunWithLoggerAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        using var temporaryFile = new TemporaryFile();
        return await RunAsync(workingDirectory, temporaryFile.Path, arguments, environmentVariables, cancellationToken);
    }

    private static string TryFindDotNetExePath()
    {
        // Adapted from https://github.com/natemcmaster/CommandLineUtils/blob/210871add72e8ad22661194c6f630fc1ecee140f/src/CommandLineUtils/Utilities/DotNetExe.cs#L1
        string fileName = "dotnet";

        bool isWindows = OperatingSystem.IsWindows();

        if (isWindows)
        {
            fileName += ".exe";
        }

        using var current = Process.GetCurrentProcess();
        var mainModule = current.MainModule;

        if (!string.IsNullOrEmpty(mainModule?.FileName) &&
            string.Equals(Path.GetFileName(mainModule.FileName), fileName, StringComparison.OrdinalIgnoreCase))
        {
            return mainModule.FileName;
        }

        return Path.Join(TryFindDotNetInstallation(), fileName);
    }

    private static ProcessStartInfo CreateDotNetStartInfo(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IDictionary<string, string?>? environmentVariables,
        string? customLoggerFileName)
    {
        var startInfo = new ProcessStartInfo(TryFindDotNetExePath(), arguments)
        {
            EnvironmentVariables =
            {
                [WellKnownEnvironmentVariables.DotNetNoLogo] = "true",
                [WellKnownEnvironmentVariables.DotNetRollForward] = "Minor",
                [BumperBuildLogger.LoggerFilePathVariableName] = customLoggerFileName,
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };

        if (environmentVariables?.Count > 0)
        {
            foreach ((var name, var value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[name] = value;
            }
        }

        // HACK See https://github.com/dotnet/msbuild/issues/6753
        startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.EnvironmentVariables["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "1";

        return startInfo;
    }

    private async Task<DotNetResult> RunAsync(
        string workingDirectory,
        string? logFilePath,
        IReadOnlyList<string> arguments,
        IDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        if (logFilePath is not null)
        {
            string loggerPath = typeof(BumperBuildLogger).Assembly.Location;
            string customLogger = $"-logger:{loggerPath}";

            arguments = [.. arguments, customLogger];
        }

        var startInfo = CreateDotNetStartInfo(workingDirectory, arguments, environmentVariables, logFilePath);

        var process = await ProcessHelper.RunAsync(startInfo, cancellationToken);

        var result = new DotNetResult(
            process.Success,
            process.ExitCode,
            process.StandardOutput,
            process.StandardError);

        if (logFilePath is not null)
        {
            result.BuildLogs = await LogReader.GetBuildLogsAsync(logFilePath, logger, CancellationToken.None);
        }

        if (!result.Success)
        {
            Log.LogCommandFailed(logger, startInfo, result);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static void LogCommandFailed(
            ILogger logger,
            ProcessStartInfo startInfo,
            ProcessResult process)
        {
            string command = startInfo.ArgumentList[0];

            CommandFailed(logger, command, process.ExitCode);

            if (!string.IsNullOrEmpty(process.StandardOutput))
            {
                CommandFailedOutput(logger, command, process.StandardOutput);
            }

            if (!string.IsNullOrEmpty(process.StandardError))
            {
                CommandFailedError(logger, command, process.StandardError);
            }
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" failed with exit code {ExitCode}.")]
        public static partial void CommandFailed(ILogger logger, string command, int exitCode);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard output: {Output}")]
        public static partial void CommandFailedOutput(ILogger logger, string command, string output);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Error,
            Message = "Command \"dotnet {Command}\" standard error: {Error}")]
        public static partial void CommandFailedError(ILogger logger, string command, string error);
    }
}
