// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper.Upgraders;

internal static class TargetFrameworkHelpers
{
    private static readonly char[] PathSeparators = ['\\', '/'];

    public static bool TryUpdateTfm(
        ReadOnlySpan<char> value,
        Version channel,
        [NotNullWhen(true)] out string? updated)
    {
        const char Delimiter = ';';

        updated = null;
        var remaining = value;

        int validTfms = 0;
        int updateableTfms = 0;

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOf(Delimiter);
            var part = index is -1 ? remaining : remaining[..index];

            if (!part.IsEmpty)
            {
                if (!part.IsTargetFrameworkMoniker())
                {
                    if (!part.StartsWith("net4") &&
                        !part.StartsWith("netstandard"))
                    {
                        return false;
                    }
                }
                else
                {
                    var version = part.ToVersionFromTargetFramework();

                    if (version is null || version >= channel)
                    {
                        return false;
                    }

                    updateableTfms++;
                }

                validTfms++;
            }

            remaining = remaining[(index + 1)..];

            if (index is -1)
            {
                break;
            }
        }

        if (updateableTfms < 1)
        {
            return false;
        }

        var newTfm = channel.ToTargetFramework();
        var append = validTfms > 1;

        if (append)
        {
            updated = new StringBuilder()
                .Append(value)
                .Append(Delimiter)
                .Append(newTfm)
                .ToString();
        }
        else
        {
            updated = newTfm;
        }

        return !value.SequenceEqual(updated);
    }

    public static bool TryUpdateTfmInPath(
        string value,
        Version channel,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (!value.Split(PathSeparators).Any((p) => p.IsTargetFrameworkMoniker()))
        {
            return false;
        }

        var builder = new StringBuilder();
        var remaining = value.AsSpan();

        bool updateValue = false;

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOfAny(PathSeparators);
            var maybeTfm = remaining;

            if (index is not -1)
            {
                maybeTfm = maybeTfm[..index];
            }

            int consumed = maybeTfm.Length;

            if (maybeTfm.IsTargetFrameworkMoniker())
            {
                if (maybeTfm.ToVersionFromTargetFramework() is { } version && version < channel)
                {
                    maybeTfm = channel.ToTargetFramework();
                    updateValue = true;
                }
            }

            builder.Append(maybeTfm);

            if (index is not -1)
            {
                builder.Append(remaining.Slice(index, 1));
                consumed++;
            }

            remaining = remaining[consumed..];
        }

        if (updateValue)
        {
            updated = builder.ToString();

            Debug.Assert(updated.Length > 0, "The updated value should have a length.");
            Debug.Assert(updated != value, "The value is was not updated.");
        }

        return updateValue;
    }
}
