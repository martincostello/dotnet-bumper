// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
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
    public async Task<(bool Success, string StdOut, string StdErr)> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = StartDotNet(workingDirectory, arguments);
        using var exited = new CancellationTokenSource();
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, exited.Token);

        var debug = _logger.IsEnabled(LogLevel.Debug);

        var command = process.StartInfo.ArgumentList[0];
        var logger = loggerFactory.CreateLogger($"dotnet {command}");

        // See https://stackoverflow.com/a/16326426/1064169
        var output = new StringBuilder(ushort.MaxValue);
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is { Length: > 0 } message)
            {
                if (debug)
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        Log.CommandOutput(logger, args.Data);
                    }
                }
                else
                {
                    output.Append(message);
                }
            }
        };

        process.BeginOutputReadLine();

        await process.WaitForExitAsync(cancellationToken);

        bool success = process.ExitCode == 0;
        string stdout = output.ToString();
        string stderr = string.Empty;

        if (!success)
        {
            stderr = await Log.LogCommandFailedAsync(_logger, process, stdout);
        }

        return (success, stdout, stderr);
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

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static async Task<string> LogCommandFailedAsync(
            ILogger logger,
            Process process,
            string output)
        {
            string command = process.StartInfo.ArgumentList[0];
            string error = string.Empty;

            Log.CommandFailed(logger, command, process.ExitCode);

            if (!logger.IsEnabled(LogLevel.Debug))
            {
                error = await process.StandardError.ReadToEndAsync(CancellationToken.None);

                if (!string.IsNullOrEmpty(output))
                {
                    Log.CommandFailedOutput(logger, command, output);
                }

                if (!string.IsNullOrEmpty(error))
                {
                    Log.CommandFailedError(logger, command, error);
                }
            }

            return error;
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" failed with exit code {ExitCode}.")]
        public static partial void CommandFailed(ILogger logger, string command, int exitCode);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "{Output}",
            SkipEnabledCheck = true)]
        public static partial void CommandOutput(ILogger logger, string output);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard output: {Output}",
            SkipEnabledCheck = true)]
        public static partial void CommandFailedOutput(ILogger logger, string command, string output);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Warning,
            Message = "Command \"dotnet {Command}\" standard error: {Error}")]
        public static partial void CommandFailedError(ILogger logger, string command, string error);
    }
}
