// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper;

internal static class RuntimeIdentifierHelpers
{
    public static bool TryUpdateRid(string value, [NotNullWhen(true)] out string? updated)
    {
        const char Delimiter = ';';

        var runtimeIds = new List<RuntimeIdentifier?>();

        updated = null;

        foreach (var part in value.Split(Delimiter))
        {
            if (part.Length is 0)
            {
                runtimeIds.Add(null);
                continue;
            }

            if (!RuntimeIdentifier.TryParse(part, out var rid))
            {
                return false;
            }

            runtimeIds.Add(rid);
        }

        if (!runtimeIds.Any((p) => p is not null))
        {
            return false;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < runtimeIds.Count; i++)
        {
            var rid = runtimeIds[i];

            if (rid is not null)
            {
                if (!rid.IsPortable)
                {
                    rid = rid.AsPortable();
                }

                builder.Append(rid.ToString());
            }

            if (i < runtimeIds.Count - 1)
            {
                builder.Append(Delimiter);
            }
        }

        var newValue = builder.ToString();
        var edited = false;

        if (!value.SequenceEqual(newValue))
        {
            edited = true;
            updated = newValue;
        }

        return edited;
    }

    public static bool TryUpdateRidInPath(string value, [NotNullWhen(true)] out string? updated)
    {
        return PathHelpers.TryUpdateValueInPath(value, Predicate, Transform, out updated);

        static bool Predicate(ReadOnlySpan<char> value) => RuntimeIdentifier.TryParse(new(value), out _);

        static bool Transform(ReadOnlySpan<char> value, out ReadOnlySpan<char> transformed)
        {
            if (TryUpdateRid(new(value), out var updated))
            {
                transformed = updated;
                return true;
            }

            transformed = value;
            return false;
        }
    }
}
