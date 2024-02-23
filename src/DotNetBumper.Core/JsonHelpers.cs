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
        CommentHandling = JsonCommentHandling.Skip, // See https://github.com/dotnet/runtime/issues/98865
    };

    public static bool TryLoadObject(string path, [NotNullWhen(true)] out JsonObject? root)
    {
        using var stream = File.OpenRead(path);
        root = JsonNode.Parse(stream, documentOptions: DocumentOptions) as JsonObject;
        return root is not null;
    }
}
