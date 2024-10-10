// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper;

internal static class JsonExtensions
{
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
        Encoding encoding;
        string newLine;

        using (var stream = FileHelpers.OpenWrite(path, out var metadata))
        {
            encoding = metadata.Encoding;
            newLine = metadata.NewLine;

#if NET9_0_OR_GREATER
            if (newLine is "\r")
            {
                // Only "\r\n" and "\n" are supported by JsonWriterOptions.NewLine
                newLine = "\n";
            }
#endif

            if (encoding.Preamble.Length > 0)
            {
                stream.Write(encoding.Preamble);
            }

#if NET9_0_OR_GREATER
            options = new JsonWriterOptions()
            {
                Encoder = options.Encoder,
                Indented = options.Indented,
                MaxDepth = options.MaxDepth,
                SkipValidation = options.SkipValidation,
                NewLine = newLine,
            };
#endif

            await using var writer = new Utf8JsonWriter(stream, options);

            node.WriteTo(writer);

            await writer.FlushAsync(cancellationToken);

            // The edit may have caused the file to shrink, so truncate it to the new length
            stream.SetLength(stream.Position);
        }

#if NET9_0_OR_GREATER
        // Append a final new line
        await File.AppendAllTextAsync(path, newLine, encoding, cancellationToken);
#else
        // JsonWriterOptions in .NET 8 does not currently support a custom NewLine character,
        // so fix up the line endings manually for by re-writing with the original.
        await FixupLineEndingsAsync(path, newLine, encoding, cancellationToken);
#endif
    }

#if NET8_0
    private static async Task FixupLineEndingsAsync(
        string path,
        string newLine,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        using var buffered = new MemoryStream();

        using (var input = File.OpenRead(path))
        using (var reader = new StreamReader(input, encoding))
        using (var writer = new StreamWriter(buffered, encoding, leaveOpen: true))
        {
            writer.NewLine = newLine;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                await writer.WriteAsync(line);
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }

        buffered.Seek(0, SeekOrigin.Begin);

        await using var output = File.OpenWrite(path);

        await buffered.CopyToAsync(output, cancellationToken);
        await buffered.FlushAsync(cancellationToken);

        output.SetLength(output.Position);
    }
#endif
}
