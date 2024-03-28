// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MartinCostello.DotNetBumper;

internal sealed partial record RuntimeIdentifier(string Value)
{
    private static readonly ImmutableDictionary<string, ImmutableHashSet<string>> PortableRids = LoadRuntimeIds("runtime-identifiers.portable");

    private static readonly ImmutableDictionary<string, ImmutableHashSet<string>> NonPortableRids = LoadRuntimeIds("runtime-identifiers");

    public bool IsPortable => PortableRids.ContainsKey(Value);

    public static MatchCollection Match(string value)
        => ContainsRid().Matches(value);

    public static bool TryParse(string? value, [NotNullWhen(true)] out RuntimeIdentifier? rid)
    {
        rid = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!PortableRids.ContainsKey(value) && !NonPortableRids.ContainsKey(value))
        {
            return false;
        }

        rid = new(value);
        return true;
    }

    public bool TryMakePortable([NotNullWhen(true)] out RuntimeIdentifier? portable)
    {
        if (IsPortable)
        {
            portable = this;
            return true;
        }

        var value = TryFindPortableRid(Value);

        return TryParse(value, out portable);

        static string? TryFindPortableRid(string value)
        {
            var candidates = new HashSet<string>();

            TryFindPortableRid(value, candidates);

            // Prefer more specific Runtime Identifiers over shorter ones (e.g. "win-x64" over "win" or "linux-musl-x64" over "linux-x64").
            // "any" is artificially the least preferred RID as it's the base of the hierarchy/graph, so is least preferred.
            return candidates
                .OrderByDescending((p) => p.Length)
                .ThenByDescending((p) => p is not "any")
                .FirstOrDefault();

            static void TryFindPortableRid(string value, HashSet<string> candidates)
            {
                if (PortableRids.ContainsKey(value))
                {
                    candidates.Add(value);
                    return;
                }

                var imports = NonPortableRids[value];

                foreach (var import in imports)
                {
                    TryFindPortableRid(import, candidates);
                }
            }
        }
    }

    public override string ToString() => Value;

    [GeneratedRegex("[a-z]+[a-z0-9\\-]+(\\.[0-9]+(\\.[0-9]+)?)?-[a-z0-9]+(\\-[a-z]+)?")]
    private static partial Regex ContainsRid();

    private static ImmutableDictionary<string, ImmutableHashSet<string>> LoadRuntimeIds(string resource)
    {
        var type = typeof(RuntimeIdentifier);
        using var stream = type.Assembly.GetManifestResourceStream($"{type.Namespace}.Resources.{resource}.json");
        var graph = JsonSerializer.Deserialize<RidGraph>(stream!, RidJsonSerializerContext.Default.RidGraph);

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>();

        foreach ((var name, var rid) in graph!.Runtimes)
        {
            builder.Add(name, [.. rid.Imports]);
        }

        return builder.ToImmutable();
    }

    private sealed class Rid
    {
        [JsonPropertyName("#import")]
        public List<string> Imports { get; set; } = [];
    }

    private sealed class RidGraph
    {
        [JsonPropertyName("runtimes")]
        public Dictionary<string, Rid> Runtimes { get; set; } = [];
    }

    [JsonSerializable(typeof(RidGraph))]
    private sealed partial class RidJsonSerializerContext : JsonSerializerContext;
}
