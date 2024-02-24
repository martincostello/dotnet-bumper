// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

/// <summary>
/// Defines an <see cref="IBumperLogWriter"/> that writes to a file.
/// </summary>
/// <param name="fileName">The path of the file to write the log to.</param>
internal abstract class FileLogWriter(string fileName) : IBumperLogWriter
{
    /// <summary>
    /// Gets the full path of the file to write the log to.
    /// </summary>
    protected string FullName { get; } = Path.GetFullPath(fileName);

    /// <inheritdoc />
    public virtual async Task WriteAsync(BumperLogContext context, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenWrite(FullName);

        await WriteLogAsync(context, stream, cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the log for the specified context.
    /// </summary>
    /// <param name="context">The <see cref="BumperLogContext"/> to generate the log from.</param>
    /// <param name="stream">The <see cref="Stream"/> to write the log content to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to generate the log.
    /// </returns>
    protected abstract Task WriteLogAsync(BumperLogContext context, Stream stream, CancellationToken cancellationToken);
}
