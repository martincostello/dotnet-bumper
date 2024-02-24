// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

internal class MarkdownLogWriter(string fileName) : FileLogWriter(fileName)
{
    protected override Task WriteLogAsync(BumperLogContext context, Stream stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
