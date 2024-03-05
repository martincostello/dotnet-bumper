// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper.Upgraders;

internal static class TargetFrameworkHelpers
{
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
        return PathHelpers.TryUpdateValueInPath(value, Predicate, Transform, out updated);

        static bool Predicate(ReadOnlySpan<char> value) => value.IsTargetFrameworkMoniker();

        bool Transform(ReadOnlySpan<char> value, out ReadOnlySpan<char> transformed)
        {
            if (value.ToVersionFromTargetFramework() is { } version && version < channel)
            {
                transformed = channel.ToTargetFramework();
                return true;
            }

            transformed = value;
            return false;
        }
    }
}
