// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper.Upgraders;

internal static class PathHelpers
{
    private static readonly char[] PathSeparators = ['\\', '/'];

    internal delegate bool Predicate(ReadOnlySpan<char> value);

    internal delegate bool Transform(ReadOnlySpan<char> value, out ReadOnlySpan<char> transformed);

    public static bool TryUpdateValueInPath(
        string value,
        Predicate predicate,
        Transform transform,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (!value.Split(PathSeparators).Any((p) => predicate(p)))
        {
            return false;
        }

        var builder = new StringBuilder();
        var remaining = value.AsSpan();

        bool edited = false;

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOfAny(PathSeparators);
            var next = remaining;

            if (index is not -1)
            {
                next = next[..index];
            }

            int consumed = next.Length;

            if (predicate(next) && transform(next, out var transformed))
            {
                next = transformed;
                edited = true;
            }

            builder.Append(next);

            if (index is not -1)
            {
                builder.Append(remaining.Slice(index, 1));
                consumed++;
            }

            remaining = remaining[consumed..];
        }

        if (edited)
        {
            updated = builder.ToString();

            Debug.Assert(updated.Length > 0, "The updated value should have a length.");
            Debug.Assert(updated != value, "The value is was not updated.");
        }

        return edited;
    }
}
