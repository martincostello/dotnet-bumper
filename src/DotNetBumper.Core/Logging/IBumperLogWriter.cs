// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

/// <summary>
/// Defines a log writer for <see cref="BumperLogContext"/> instances.
/// </summary>
internal interface IBumperLogWriter
{
    /// <summary>
    /// Writes the log for the specified context.
    /// </summary>
    /// <param name="context">The <see cref="BumperLogContext"/> to write the log from.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to write the log.
    /// </returns>
    Task WriteAsync(BumperLogContext context, CancellationToken cancellationToken);
}
