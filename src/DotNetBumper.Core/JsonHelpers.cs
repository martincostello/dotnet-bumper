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

    public static bool TryLoadObjectFromString(string json, [NotNullWhen(true)] out JsonObject? root)
    {
        root = JsonNode.Parse(json, documentOptions: DocumentOptions) as JsonObject;
        return root is not null;
    }

    public static bool UpdateStringNodes<T>(JsonObject root, T state, Func<JsonValue, T, bool> processValue)
    {
        return Visit(root, state, processValue);

        static bool Visit(JsonObject node, T state, Func<JsonValue, T, bool> processValue)
        {
            bool edited = false;

            foreach (var property in node.ToArray())
            {
                if (property.Value is JsonValue value)
                {
                    if (value.GetValueKind() is JsonValueKind.String)
                    {
                        edited |= processValue(value, state);
                    }
                }
                else if (property.Value is JsonArray array)
                {
                    foreach (var item in array.ToArray())
                    {
                        if (item is JsonObject obj)
                        {
                            edited |= Visit(obj, state, processValue);
                        }
                        else if (item is JsonValue arrayValue && arrayValue.GetValueKind() is JsonValueKind.String)
                        {
                            edited |= processValue(arrayValue, state);
                        }
                    }
                }
                else if (property.Value is JsonObject obj)
                {
                    edited |= Visit(obj, state, processValue);
                }
            }

            return edited;
        }
    }
}
