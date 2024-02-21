// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper;

internal static class JsonHelpers
{
    internal static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static bool TryLoadObject(
        string path,
        [NotNullWhen(true)] out JsonObject? root)
    {
        using var stream = File.OpenRead(path);
        return TryLoadObject(stream, out root);
    }

    public static bool TryLoadObject(
        string path,
        [NotNullWhen(true)] out JsonObject? root,
        [NotNullWhen(true)] out FileMetadata? metadata)
    {
        using var stream = FileHelpers.OpenRead(path, out metadata);
        return TryLoadObject(stream, out root);
    }

    private static bool TryLoadObject(
        Stream stream,
        [NotNullWhen(true)] out JsonObject? root)
    {
        root = JsonNode.Parse(stream, documentOptions: DocumentOptions) as JsonObject;
        return root is not null;
    }
}
