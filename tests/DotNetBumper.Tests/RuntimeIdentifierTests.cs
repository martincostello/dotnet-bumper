// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class RuntimeIdentifierTests
{
    internal static readonly string[] MacOSVersions = ["10.10", "10.11", "10.12", "10.13", "10.14", "10.15", "10.16", "11.0", "12", "13"];
    internal static readonly string[] WindowsVersions = ["7", "8", "81", "10"];

    public static TheoryData<string, bool, bool?> RuntimeIdentifiers()
    {
        var testCases = new TheoryData<string, bool, bool?>
        {
            { string.Empty, false, null },
            { " ", false, null },
            { ";", false, null },
            { ";;", false, null },
            { "multi-targeting", false, null },
            { "--configuration", false, null },
            { "--framework", false, null },
            { "--runtime", false, null },
            { "alpine-x64", true, false },
            { "android-arm64", true,  true },
            { "arch-x64", true, false },
            { "browser-wasm", true, true },
            { "centos-x64", true, false },
            { "debian-x64", true, false },
            { "fedora-x64", true, false },
            { "freebsd-x64", true, true },
            { "gentoo-x64", true, false },
            { "haiku-x64", true,  true },
            { "illumos-x64", true, true },
            { "ios-arm64", true, true },
            { "iossimulator-arm64", true, true },
            { "linux-arm", true, true },
            { "linux-arm64", true, true },
            { "linux-musl-x64", true, true },
            { "linux-musl-arm64", true, true },
            { "linux-x64", true, true },
            { "linuxmint.17.1-x64", true, false },
            { "maccatalyst-arm64", true, true },
            { "miraclelinux-x64", true, false },
            { "ol-x64", true, false },
            { "omnios-x64", true, false },
            { "openindiana-x64", true, false },
            { "opensuse-x64", true, false },
            { "osx-arm64", true, true },
            { "osx-x64", true, true },
            { "rhel-x64", true, false },
            { "rocky-x64", true, false },
            { "sles-x64", true, false },
            { "smartos-x64", true, false },
            { "solaris-x64", true, true },
            { "tizen-arm64", true, false },
            { "tvos-arm64", true, true },
            { "tvossimulator-arm64", true, true },
            { "ubuntu-x64", true, false },
            { "ubuntu.16.04-x64", true, false },
            { "unix-x64", true, true },
            { "wasi-wasm", true, true },
            { "win", true, true },
            { "win-arm64", true, true },
            { "win-x64", true, true },
            { "win-x86", true, true },
        };

        foreach (var version in WindowsVersions)
        {
            testCases.Add($"win{version}-arm", true, false);
            testCases.Add($"win{version}-arm64", true, false);
            testCases.Add($"win{version}-x64", true, false);
            testCases.Add($"win{version}-x86", true, false);
        }

        foreach (var version in MacOSVersions)
        {
            testCases.Add($"osx.{version}-arm64", true, false);
            testCases.Add($"osx.{version}-x64", true, false);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(RuntimeIdentifiers))]
    public static void TryParse_Returns_Correct_Value(
        string value,
        bool expectedResult,
        bool? expectedIsPortable)
    {
        // Act
        bool actualResult = RuntimeIdentifier.TryParse(value, out var actualRid);

        // Assert
        actualResult.ShouldBe(expectedResult);

        if (expectedResult)
        {
            actualRid.ShouldNotBeNull();
            actualRid.Value.ShouldBe(value);
            actualRid.IsPortable.ShouldBe(expectedIsPortable.GetValueOrDefault());
            actualRid.ToString().ShouldBe(value);
        }
        else
        {
            actualRid.ShouldBeNull();
        }
    }
}
