// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.DotNetBumper;

internal static class ProcessHelper
{
    private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(10);

    public static Task<ProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
        => RunAsync(startInfo, DefaultDrainTimeout, cancellationToken);

    internal static async Task<ProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan drainTimeout,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start process for {startInfo.FileName}.");

        // The redirected streams are read concurrently with waiting for the process to exit to avoid a deadlock
        // if the process fills the buffer of either stream. The content is captured incrementally as it is read
        // so that any output that has already been read is not lost if reading has to be stopped before the end
        // of the stream is reached (e.g. because a child process is still holding the stream open).
        // See https://stackoverflow.com/a/16326426/1064169 and
        // https://learn.microsoft.com/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput.
        using var readerTokenSource = new CancellationTokenSource();

        var error = new OutputBuffer();
        var output = new OutputBuffer();

        var readError = ConsumeStreamAsync(process.StartInfo.RedirectStandardError ? process.StandardError : null, error, readerTokenSource.Token);
        var readOutput = ConsumeStreamAsync(process.StartInfo.RedirectStandardOutput ? process.StandardOutput : null, output, readerTokenSource.Token);

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

        // The process has exited, but any output it wrote just before exiting may not have
        // been read yet, so wait for the streams to be read to the end before using the output.
        // The streams are not cancelled as soon as the process exits, as that could cause
        // output that was written by the process but not yet read to be lost.
        try
        {
            await Task.WhenAll(readError, readOutput).WaitAsync(drainTimeout, CancellationToken.None);
        }
        catch (TimeoutException)
        {
            // Stop reading the streams and use whatever output has been captured so far
            await readerTokenSource.CancelAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessResult(
            process.ExitCode == 0,
            process.ExitCode,
            output.ToString(),
            error.ToString());
    }

    private static async Task ConsumeStreamAsync(
        StreamReader? reader,
        OutputBuffer buffer,
        CancellationToken cancellationToken)
    {
        if (reader is null)
        {
            return;
        }

        // Yield so that the stream is read on another thread rather than the
        // caller's, as the stream may be a blocking synchronous stream on Windows.
        await Task.Yield();

        var chunk = new char[4096];

        try
        {
            int count;

            while ((count = await reader.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
            {
                buffer.Append(chunk.AsSpan(0, count));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Ignore - reading was stopped or the stream was closed
        }
    }

    private sealed class OutputBuffer
    {
        private readonly StringBuilder _builder = new();

        public void Append(ReadOnlySpan<char> value)
        {
            lock (_builder)
            {
                _builder.Append(value);
            }
        }

        public override string ToString()
        {
            lock (_builder)
            {
                return _builder.ToString();
            }
        }
    }
}
