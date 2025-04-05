// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.DotNetBumper;

internal static class ProcessHelper
{
    public static async Task<ProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for {startInfo.FileName}.");

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

        var result = new ProcessResult(
            process.ExitCode == 0,
            process.ExitCode,
            output,
            error);

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
                Task.Run(() => ProcessStream(reader, cancellationToken), cancellationToken) :
                Task.FromResult(new StringBuilder(0));

            static async Task<StringBuilder> ProcessStream(
                StreamReader reader,
                CancellationToken cancellationToken)
            {
                var builder = new StringBuilder();

                try
                {
                    builder.Append(await reader.ReadToEndAsync(cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                }

                return builder;
            }
        }
    }
}
