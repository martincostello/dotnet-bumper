// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MartinCostello.DotNetBumper;

[ExcludeFromCodeCoverage]
[JsonSerializable(typeof(BumperConfiguration))]
[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip)]
internal sealed partial class BumperJsonSerializerContext : JsonSerializerContext;
