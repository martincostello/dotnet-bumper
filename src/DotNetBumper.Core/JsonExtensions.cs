// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper;

internal static class JsonExtensions
{
    private static readonly byte[] NewLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);

    public static bool TryGetStringProperty(
        this JsonObject node,
        string propertyName,
        [NotNullWhen(true)] out JsonNode? property,
        [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (node.TryGetPropertyValue(propertyName, out property) &&
            property is not null &&
            property.GetValueKind() is JsonValueKind.String)
        {
            value = property.GetValue<string>();
            return true;
        }

        return false;
    }

    public static async Task SaveAsync(
        this JsonObject node,
        string path,
        CancellationToken cancellationToken)
    {
        var options = new JsonWriterOptions() { Indented = true };
        await node.SaveAsync(path, options, cancellationToken);
    }

    public static async Task SaveAsync(
        this JsonObject node,
        string path,
        JsonWriterOptions options,
        CancellationToken cancellationToken)
    {
        using var stream = FileHelpers.OpenWrite(path, out var metadata);

        if (metadata.Encoding.Preamble.Length > 0)
        {
            stream.Write(metadata.Encoding.Preamble);
        }

        // JsonWriterOptions does not currently support a custom NewLine character
        using var writer = new Utf8JsonWriter(stream, options);

        node.WriteTo(writer);

        await writer.FlushAsync(cancellationToken);
        await stream.WriteAsync(NewLineBytes, cancellationToken);

        // The edit may have caused the file to shrink, so truncate it to the new length
        stream.SetLength(stream.Position);
    }
}
