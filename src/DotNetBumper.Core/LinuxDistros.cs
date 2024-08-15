// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace MartinCostello.DotNetBumper;

internal static class LinuxDistros
{
    private static readonly FrozenSet<string> UbuntuCodeNames = new[] { "focal", "jammy", "noble" }.ToFrozenSet();

    public static ReadOnlySpan<char> TryUpdateDistro(Version channel, ReadOnlySpan<char> distro)
    {
        return channel.Major switch
        {
            8 => TryUpdateDistro(distro, ["bookworm"], ["jammy", "noble"], ["19", "20"], updateMariner2: false),
            _ => TryUpdateDistro(distro, ["bookworm"], ["noble"], ["20"], updateMariner2: true), // Latest known versions as of .NET 9
        };

        static ReadOnlySpan<char> TryUpdateDistro(
            ReadOnlySpan<char> distro,
            string[] debian,
            string[] ubuntu,
            string[] alpine,
            bool updateMariner2)
        {
            // Is it a Debian image?
            int index = distro.IndexOf("-slim");

            if (index > -1)
            {
                return TryUpgradeDistro(distro, index, debian);
            }

            // Is it an Alpine 3 image?
            const string Alpine3 = "alpine3";
            if (distro.StartsWith(Alpine3) &&
                distro.Length > (Alpine3.Length + 1) &&
                distro[Alpine3.Length] is '.')
            {
                var maybeVersion = distro[(Alpine3.Length + 1)..];

                if (maybeVersion.Length == 2 || (maybeVersion.Length > 3 && maybeVersion[2] is '-'))
                {
                    var suffix = TryUpgradeDistro(maybeVersion, 2, alpine);
                    return $"{Alpine3}.{suffix}";
                }

                return distro;
            }

            // Maybe it's Ubuntu
            foreach (var codeName in UbuntuCodeNames)
            {
                if (distro.StartsWith(codeName, StringComparison.Ordinal))
                {
                    return TryUpgradeDistro(distro, codeName.Length, ubuntu);
                }
            }

            // Does Mariner need updating to Azure Linux 3?
            const string Mariner = "cbl-mariner";
            if (updateMariner2 && distro.StartsWith(Mariner, StringComparison.Ordinal))
            {
                var suffix = distro[Mariner.Length..];

                if (suffix.Length >= 3 && suffix[0..3].SequenceEqual("2.0"))
                {
                    suffix = suffix[3..];
                }

                return $"azurelinux3.0{suffix}";
            }

            return distro;
        }

        static ReadOnlySpan<char> TryUpgradeDistro(ReadOnlySpan<char> distro, int index, string[] candidates)
        {
            var name = new string(distro[..index]);
            var suffix = distro[index..];

            if (candidates.Contains(name, StringComparer.Ordinal))
            {
                // Using an existing supported version
                return distro;
            }

            // Use the first supported version
            return $"{candidates[0]}{suffix}";
        }
    }
}
