// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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

    private static Process StartDotNet(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IDictionary<string, string?>? environmentVariables,
        string? customLoggerFileName)
    {
        var startInfo = new ProcessStartInfo(DotNetExe.FullPathOrDefault(), arguments)
        {
            EnvironmentVariables =
            {
                [WellKnownEnvironmentVariables.DotNetNoLogo] = "true",
                [WellKnownEnvironmentVariables.DotNetRollForward] = "Minor",
                [WellKnownEnvironmentVariables.MSBuildSdksPath] = null,
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

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for dotnet {arguments[0]}.");
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

        using var process = StartDotNet(workingDirectory, arguments, environmentVariables, logFilePath);

        // See https://stackoverflow.com/a/16326426/1064169 and
        // https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput.
        using var outputTokenSource = new CancellationTokenSource();
        var readOutput = ReadOutputAsync(process, outputTokenSource.Token);

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

        (string error, string output) = await readOutput;

        var result = new DotNetResult(
            process.ExitCode == 0,
            process.ExitCode,
            output,
            error);

        if (logFilePath is not null)
        {
            result.BuildLogs = await LogReader.GetBuildLogsAsync(logFilePath, logger, CancellationToken.None);
        }

        if (!result.Success)
        {
            Log.LogCommandFailed(logger, process, result.StandardOutput, result.StandardError);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return result;

        static async Task<(string Error, string Output)> ReadOutputAsync(
            Process process,
            CancellationToken cancellationToken)
        {
            var processErrors = ConsumeStreamAsync(process.StandardError, process.StartInfo.RedirectStandardError, cancellationToken);
            var processOutput = ConsumeStreamAsync(process.StandardOutput, process.StartInfo.RedirectStandardOutput, cancellationToken);

            await Task.WhenAll(processErrors, processOutput);

            string error = string.Empty;
            string output = string.Empty;

            if (processErrors.Status == TaskStatus.RanToCompletion)
            {
                error = (await processErrors).ToString();
            }

            if (processOutput.Status == TaskStatus.RanToCompletion)
            {
                output = (await processOutput).ToString();
            }

            return (error, output);
        }

        static Task<StringBuilder> ConsumeStreamAsync(
            StreamReader reader,
            bool isRedirected,
            CancellationToken cancellationToken)
        {
            return isRedirected ?
                Task.Run<StringBuilder>(() => ProcessStream(reader, cancellationToken), cancellationToken) :
                Task.FromResult(new StringBuilder(0));

            static async Task<StringBuilder> ProcessStream(
                StreamReader reader,
                CancellationToken cancellationToken)
            {
                var builder = new StringBuilder();

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        builder.Append(await reader.ReadToEndAsync(cancellationToken));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                return builder;
            }
        }
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
