// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper.Logging;

internal sealed class JsonLogFormatter(string fileName) : FileLogWriter(fileName)
{
    protected override async Task WriteLogAsync(BumperLogContext context, Stream stream, CancellationToken cancellationToken)
    {
        var document = CreateDocument(context);

        var writer = new Utf8JsonWriter(stream);

        document.WriteTo(writer);

        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static JsonObject CreateDocument(BumperLogContext context)
    {
        var document = new JsonObject()
        {
            ["startedAt"] = JsonValue.Create(context.StartedAt),
            ["finishedAt"] = JsonValue.Create(context.FinishedAt),
        };

        return document;
    }
}
