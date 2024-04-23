// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Nodes;

internal static class JsonNodeExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = true,
    };

    public static string PrettyPrint(this JsonNode node)
        => node.ToJsonString(SerializerOptions);
}
