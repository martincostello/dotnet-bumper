// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// Runs a .NET process.
/// </summary>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use.</param>
public sealed partial class DotNetProcess(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DotNetProcess>();

    /// <summary>
    /// Runs the specified dotnet command.
    /// </summary>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="arguments">The arguments to the command.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to run the command
    /// that returns <see langword="true"/> if the process exited with an exit code
    /// of <c>0</c>; otherwise <see langword="false"/>.
    /// </returns>
    public async Task<bool> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = StartDotNet(workingDirectory, arguments);
        using var exited = new CancellationTokenSource();
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, exited.Token);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            process.Exited += (_, _) => exited.Cancel();

            var command = process.StartInfo.ArgumentList[0];
            var outputLogger = loggerFactory.CreateLogger($"dotnet {command}");

            StreamLogs(process.StandardOutput, outputLogger, LogLevel.Debug, combined.Token);
            StreamLogs(process.StandardError, outputLogger, LogLevel.Error, combined.Token);
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            await Log.LogCommandFailedAsync(_logger, process);
            return false;
        }

        return true;
    }

    private static Process StartDotNet(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(DotNetExe.FullPathOrDefault(), arguments)
        {
            EnvironmentVariables =
            {
                ["DOTNET_ROLL_FORWARD"] = "Major",
                ["MSBuildSDKsPath"] = null,
            },
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for dotnet {arguments[0]}.");
    }

    private static void StreamLogs(
        StreamReader output,
        ILogger logger,
        LogLevel level,
        CancellationToken cancellationToken)
    {
        _ = Task.Factory.StartNew(
            async (state) =>
            {
                var args = ((StreamReader Output, ILogger Logger, LogLevel Level, CancellationToken Token))state!;
                await ReadStreamAsync(args.Output, args.Logger, args.Level, args.Token);
            },
            (output, logger, level, cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        static async Task ReadStreamAsync(
            StreamReader output,
            ILogger logger,
            LogLevel level,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await output.ReadLineAsync(cancellationToken);

                    if (line is not null)
                    {
#pragma warning disable CA1848 // Use the LoggerMessage delegates
                        logger.Log(level, "{Message}", line);
#pragma warning restore CA1848 // Use the LoggerMessage delegates
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static async Task LogCommandFailedAsync(ILogger logger, Process process)
        {
            string command = process.StartInfo.ArgumentList[0];

            Log.CommandFailed(logger, command, process.ExitCode);

            if (!logger.IsEnabled(LogLevel.Debug))
            {
                string output = await process.StandardOutput.ReadToEndAsync(CancellationToken.None);
                string error = await process.StandardError.ReadToEndAsync(CancellationToken.None);

                if (!string.IsNullOrEmpty(output))
                {
                    Log.CommandFailedOutput(logger, command, output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Log.CommandFailedError(logger, command, error);
                }
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
}
