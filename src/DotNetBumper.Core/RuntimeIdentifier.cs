// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MartinCostello.DotNetBumper;

internal sealed partial record RuntimeIdentifier(
    string OperatingSystem,
    string Version,
    string Architecture,
    string AdditionalQualifiers)
{
    private const string RidPattern = "(?<os>((alpine|android|arch|browser|centos|debian|fedora|freebsd|gentoo|haiku|illumos|ios|iossimulator|linux|linuxmint|maccatalyst|miraclelinux|ol|omnios|openindiana|opensuse|osx|rhel|rocky|sles|smartos|solaris|tizen|tvos|tvossimulator|ubuntu|unix|wasi|win([0-9]+)?)(\\-[a-z\\-]+)?))(\\.(?<version>([0-9]+)((\\.[0-9]+))?))?-(?<architecture>[a-z0-9]+)(?<qualifiers>\\-[a-z]+)?";

    /// <summary>
    /// In the future, it would be better to dynamically generate this map from the sources of truth:
    /// <list type="bullet">
    /// <item>https://github.com/dotnet/sdk/blob/main/src/Layout/redist/PortableRuntimeIdentifierGraph.json</item>
    /// <item>https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.NETCore.Platforms/src/runtime.json</item>
    /// </list>
    /// </summary>
    private static readonly ImmutableDictionary<string, string> PortableRidMap = new Dictionary<string, string>()
    {
        ["alpine"] = "linux-musl",
        ["arch"] = "linux",
        ["centos"] = "linux",
        ["debian"] = "linux",
        ["fedora"] = "linux",
        ["gentoo"] = "linux",
        ["linuxmint"] = "linux",
        ["miraclelinux"] = "linux",
        ["ol"] = "linux",
        ["omnios"] = "illumos",
        ["openindiana"] = "illumos",
        ["opensuse"] = "linux",
        ["rhel"] = "linux",
        ["rocky"] = "linux",
        ["sles"] = "linux",
        ["smartos"] = "illumos",
        ["tizen"] = "linux",
        ["ubuntu"] = "linux",
        ["win7"] = "win",
        ["win8"] = "win",
        ["win81"] = "win",
        ["win10"] = "win",
    }.ToImmutableDictionary();

    public bool IsPortable => Version is { Length: 0 } && !PortableRidMap.ContainsKey(OperatingSystem);

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

        if (PortableRidMap.TryGetValue(os, out var portable))
        {
            os = portable;
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
