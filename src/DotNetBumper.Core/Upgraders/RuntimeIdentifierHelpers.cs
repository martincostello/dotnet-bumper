// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MartinCostello.DotNetBumper.Upgraders;

internal static class RuntimeIdentifierHelpers
{
    private static readonly char[] PathSeparators = ['\\', '/'];

    private static readonly SearchValues<char> VersionCharacters = SearchValues.Create("0123456789.");

    public static bool TryUpdateRid(
        string value,
        [NotNullWhen(true)] out string? updated)
    {
        const char Delimiter = ';';

        var runtimeIds = new List<string?>();

        updated = null;

        foreach (var part in value.Split(Delimiter))
        {
            if (part.Length is 0)
            {
                runtimeIds.Add(null);
                continue;
            }

            var rid = part;
            int startIndex = rid.IndexOf('.', StringComparison.Ordinal);
            int endIndex = -1;

            const string WindowsPrefix = "win";

            if (startIndex is not -1)
            {
                endIndex = rid.IndexOf('-', startIndex);
            }
            else if (rid.StartsWith(WindowsPrefix, StringComparison.Ordinal))
            {
                startIndex = WindowsPrefix.Length;
                var versionIndex = rid[startIndex..].AsSpan().IndexOfAnyExcept(VersionCharacters);

                if (versionIndex is not -1)
                {
                    endIndex = versionIndex + WindowsPrefix.Length;
                }
            }

            if (startIndex is not -1)
            {
                var prefix = rid[..startIndex];

                if (endIndex is -1)
                {
                    rid = prefix;
                }
                else
                {
                    rid = new StringBuilder(prefix)
                        .Append(rid[endIndex..])
                        .ToString();
                }
            }

            runtimeIds.Add(rid);
        }

        if (!runtimeIds.Any((p) => p is not null))
        {
            return false;
        }

        var builder = new StringBuilder();

        for (int i = 0; i < runtimeIds.Count; i++)
        {
            var rid = runtimeIds[i];

            if (rid is { Length: > 0 })
            {
                builder.Append(rid);
            }

            if (i < runtimeIds.Count - 1)
            {
                builder.Append(Delimiter);
            }
        }

        var newValue = builder.ToString();
        bool edited = false;

        if (!value.SequenceEqual(newValue))
        {
            edited = true;
            updated = newValue;
        }

        return edited;
    }

    public static bool TryUpdateRidInPath(
        string value,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (!PathSeparators.Any(value.Contains))
        {
            return false;
        }

        bool edited = false;

        var builder = new StringBuilder();
        var remaining = value.AsSpan();

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOfAny(PathSeparators);
            var maybeRid = remaining;

            if (index is not -1)
            {
                maybeRid = maybeRid[..index];
            }

            int consumed = maybeRid.Length;

            if (TryUpdateRid(new(maybeRid), out var updatedRid))
            {
                maybeRid = updatedRid;
                edited = true;
            }

            builder.Append(maybeRid);

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

    private sealed record RuntimeIdentifier(
        string OperatingSystem,
        string? Version,
        string? Architecture,
        string? AdditionalQualifiers)
    {
        public bool IsPortable => Version is null && AdditionalQualifiers is null;

        public static bool TryParse(string value, [NotNullWhen(true)] out RuntimeIdentifier? rid)
        {
            rid = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 1)
            {
                return false;
            }

            var operatingSystem = parts[0];
            var architecture = parts.ElementAtOrDefault(1);
            var additionalQualifiers = parts.ElementAtOrDefault(2);

            string? version = null;

            if (operatingSystem.Contains('.', StringComparison.Ordinal))
            {
                parts = operatingSystem.Split('.', StringSplitOptions.RemoveEmptyEntries);
                version = parts.ElementAtOrDefault(1);
            }

            rid = new(
                operatingSystem,
                version,
                architecture,
                additionalQualifiers);

            return true;
        }

        public RuntimeIdentifier AsPortable() => this with { Version = null, AdditionalQualifiers = null };

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(OperatingSystem);

            if (Version is { Length: > 0 })
            {
                builder.Append('.');
                builder.Append(Version);
            }

            if (Architecture is { Length: > 0 })
            {
                builder.Append('-');
                builder.Append(Architecture);
            }

            if (AdditionalQualifiers is { Length: > 0 })
            {
                builder.Append('-');
                builder.Append(AdditionalQualifiers);
            }

#pragma warning disable CA1308
            return builder.ToString().ToLowerInvariant();
#pragma warning restore CA1308
        }
    }
}
