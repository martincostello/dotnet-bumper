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
/// <param name="logger">The <see cref="ILogger{DotNetProcess}"/> to use.</param>
public sealed partial class DotNetProcess(ILogger<DotNetProcess> logger)
{
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
        using var process = StartDotNet(workingDirectory, arguments);

        var error = new StringBuilder();
        var output = new StringBuilder();

        // See https://stackoverflow.com/a/16326426/1064169 and
        // https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput.
        using var outputTokenSource = new CancellationTokenSource();
        var processOutput = ConsumeStreamAsync(process.StandardError, error, outputTokenSource.Token);
        var processErrors = ConsumeStreamAsync(process.StandardOutput, output, outputTokenSource.Token);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        finally
        {
            await outputTokenSource.CancelAsync();
        }

        await Task.WhenAll([processOutput, processErrors]);

        var result = new DotNetResult(
            process.ExitCode == 0,
            output.ToString(),
            error.ToString());

        if (!result.Success)
        {
            Log.LogCommandFailed(logger, process, result.StandardOutput, result.StandardError);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return result;

        static Task ConsumeStreamAsync(
            StreamReader reader,
            StringBuilder output,
            CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () => ProcessStream(reader, output, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            static async Task ProcessStream(
                StreamReader reader,
                StringBuilder output,
                CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        output.Append(await reader.ReadToEndAsync(cancellationToken));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
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

        // HACK See https://github.com/dotnet/msbuild/issues/6753
        startInfo.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1";
        startInfo.EnvironmentVariables["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "1";

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for dotnet {arguments[0]}.");
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        public static void LogCommandFailed(
            ILogger logger,
            Process process,
            string output,
            string error)
        {
            string command = process.StartInfo.ArgumentList[0];

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
