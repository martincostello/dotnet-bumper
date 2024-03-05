// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MartinCostello.DotNetBumper.Upgraders;

internal static partial class RuntimeIdentifierHelpers
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

        for (int i = 0; i < runtimeIds.Count; i++)
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
        bool edited = false;

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

    private sealed partial record RuntimeIdentifier(
        string OperatingSystem,
        string Version,
        string Architecture,
        string AdditionalQualifiers)
    {
        private const string Windows = "win";

        public bool IsPortable =>
            Version is null &&
            (OperatingSystem is Windows || !OperatingSystem.StartsWith(Windows, StringComparison.Ordinal));

        public static bool TryParse(string value, [NotNullWhen(true)] out RuntimeIdentifier? rid)
        {
            rid = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = Rid().Match(value);

            if (!match.Success)
            {
                return false;
            }

            rid = new(
                match.Groups["os"].Value,
                match.Groups["version"].Value,
                match.Groups["architecture"].Value,
                match.Groups["qualifiers"].Value);

            return true;
        }

        public RuntimeIdentifier AsPortable()
        {
            string os = OperatingSystem;

            if (os.StartsWith(Windows, StringComparison.Ordinal) && os.Length > Windows.Length)
            {
                os = Windows;
            }

            return this with
            {
                OperatingSystem = os,
                Version = string.Empty,
            };
        }

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

            return builder.ToString();
        }

        [GeneratedRegex($"^(?<os>[a-z0-9\\-]+)(?<version>(\\.[0-9]+)+)?-(?<architecture>[a-z0-9]+)(?<extra>\\-[a-z]+)?$")]
        private static partial Regex Rid();
    }
}
