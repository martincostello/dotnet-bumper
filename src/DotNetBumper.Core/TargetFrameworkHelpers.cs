// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper;

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
        var updateableTfms = new List<string>();

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

                    updateableTfms.Add(part.ToString());
                }

                validTfms++;
            }

            remaining = remaining[(index + 1)..];

            if (index is -1)
            {
                break;
            }
        }

        if (updateableTfms.Count < 1)
        {
            return false;
        }

        var newTfm = channel.ToTargetFramework();

        if (validTfms > 1)
        {
            var prefix = value;
            var suffix = newTfm.AsSpan();

            // Insert the new TFM in the correct order based on the existing sorting of the TFMs
            if (updateableTfms.Count > 1)
            {
                var firstVersion = updateableTfms[0].ToVersionFromTargetFramework();
                var secondVersion = updateableTfms[1].ToVersionFromTargetFramework();

                if (firstVersion > secondVersion)
                {
                    prefix = newTfm;
                    suffix = value;
                }
            }

            updated = new StringBuilder()
                .Append(prefix)
                .Append(Delimiter)
                .Append(suffix)
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
