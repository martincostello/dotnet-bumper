// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MartinCostello.DotNetBumper;

internal sealed partial record RuntimeIdentifier(
    string OperatingSystem,
    string Version,
    string Architecture,
    string AdditionalQualifiers)
{
    private const string RidPattern = "(?<os>((android|ios|linux|osx|win([0-9]+)?)(\\-[a-z\\-]+)?))(\\.(?<version>([0-9]+)((\\.[0-9]+))?))?-(?<architecture>[a-z0-9]+)(?<qualifiers>\\-[a-z]+)?";

    private const string Windows = "win";

    public bool IsPortable =>
        Version is { Length: 0 } &&
        (OperatingSystem is Windows || !OperatingSystem.StartsWith(Windows, StringComparison.Ordinal));

    public static MatchCollection Match(string value)
        => ContainsRid().Matches(value);

    public static bool TryParse(string value, [NotNullWhen(true)] out RuntimeIdentifier? rid)
    {
        rid = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = IsRid().Match(value);

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

    [GeneratedRegex($"{RidPattern}")]
    private static partial Regex ContainsRid();

    [GeneratedRegex($"^{RidPattern}$")]
    private static partial Regex IsRid();
}
