// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class RuntimeIdentifierTests
{
    internal static readonly string[] MacOSVersions = ["10.10", "10.11", "10.12", "10.13", "10.14", "10.15", "10.16", "11.0", "12", "13"];
    internal static readonly string[] WindowsVersions = ["7", "8", "81", "10"];

    public static TheoryData<string, bool, string?, string?, string?, string?, bool?> RuntimeIdentifiers()
    {
        var testCases = new TheoryData<string, bool, string?, string?, string?, string?, bool?>
        {
            { string.Empty, false, null, null, null, null, null },
            { " ", false, null, null, null, null, null },
            { ";", false, null, null, null, null, null },
            { ";;", false, null, null, null, null, null },
            { "multi-targeting", false, null, null, null, null, null },
            { "--configuration", false, null, null, null, null, null },
            { "--framework", false, null, null, null, null, null },
            { "--runtime", false, null, null, null, null, null },
            { "alpine-x64", true, "alpine", string.Empty, "x64", string.Empty, false },
            { "android-arm64", true, "android", string.Empty, "arm64", string.Empty, true },
            { "arch-x64", true, "arch", string.Empty, "x64", string.Empty, false },
            { "browser-wasm", true, "browser", string.Empty, "wasm", string.Empty, true },
            { "centos-x64", true, "centos", string.Empty, "x64", string.Empty, false },
            { "debian-x64", true, "debian", string.Empty, "x64", string.Empty, false },
            { "fedora-x64", true, "fedora", string.Empty, "x64", string.Empty, false },
            { "freebsd-x64", true, "freebsd", string.Empty, "x64", string.Empty, true },
            { "gentoo-x64", true, "gentoo", string.Empty, "x64", string.Empty, false },
            { "haiku-x64", true, "haiku", string.Empty, "x64", string.Empty, true },
            { "illumos-x64", true, "illumos", string.Empty, "x64", string.Empty, true },
            { "ios-arm64", true, "ios", string.Empty, "arm64", string.Empty, true },
            { "iossimulator-arm64", true, "iossimulator", string.Empty, "arm64", string.Empty, true },
            { "linux-arm", true, "linux", string.Empty, "arm", string.Empty, true },
            { "linux-arm64", true, "linux", string.Empty, "arm64", string.Empty, true },
            { "linux-musl-x64", true, "linux-musl", string.Empty, "x64", string.Empty, true },
            { "linux-musl-arm64", true, "linux-musl", string.Empty, "arm64", string.Empty, true },
            { "linux-x64", true, "linux", string.Empty, "x64", string.Empty, true },
            { "linuxmint.17.1-x64", true, "linuxmint", "17.1", "x64", string.Empty, false },
            { "maccatalyst-arm64", true, "maccatalyst", string.Empty, "arm64", string.Empty, true },
            { "miraclelinux-x64", true, "miraclelinux", string.Empty, "x64", string.Empty, false },
            { "ol-x64", true, "ol", string.Empty, "x64", string.Empty, false },
            { "omnios-x64", true, "omnios", string.Empty, "x64", string.Empty, false },
            { "openindiana-x64", true, "openindiana", string.Empty, "x64", string.Empty, false },
            { "opensuse-x64", true, "opensuse", string.Empty, "x64", string.Empty, false },
            { "osx-arm64", true, "osx", string.Empty, "arm64", string.Empty, true },
            { "osx-x64", true, "osx", string.Empty, "x64", string.Empty, true },
            { "rhel-x64", true, "rhel", string.Empty, "x64", string.Empty, false },
            { "rocky-x64", true, "rocky", string.Empty, "x64", string.Empty, false },
            { "sles-x64", true, "sles", string.Empty, "x64", string.Empty, false },
            { "smartos-x64", true, "smartos", string.Empty, "x64", string.Empty, false },
            { "solaris-x64", true, "solaris", string.Empty, "x64", string.Empty, true },
            { "tizen-arm64", true, "tizen", string.Empty, "arm64", string.Empty, false },
            { "tvos-arm64", true, "tvos", string.Empty, "arm64", string.Empty, true },
            { "tvossimulator-arm64", true, "tvossimulator", string.Empty, "arm64", string.Empty, true },
            { "ubuntu-x64", true, "ubuntu", string.Empty, "x64", string.Empty, false },
            { "ubuntu.16.04-x64", true, "ubuntu", "16.04", "x64", string.Empty, false },
            { "unix-x64", true, "unix", string.Empty, "x64", string.Empty, true },
            { "wasi-wasm", true, "wasi", string.Empty, "wasm", string.Empty, true },
            { "win-arm64", true, "win", string.Empty, "arm64", string.Empty, true },
            { "win-x64", true, "win", string.Empty, "x64", string.Empty, true },
            { "win-x86", true, "win", string.Empty, "x86", string.Empty, true },
        };

        foreach (var version in WindowsVersions)
        {
            testCases.Add($"win{version}-arm", true, $"win{version}", string.Empty, "arm", string.Empty, false);
            testCases.Add($"win{version}-arm64", true, $"win{version}", string.Empty, "arm64", string.Empty, false);
            testCases.Add($"win{version}-x64", true, $"win{version}", string.Empty, "x64", string.Empty, false);
            testCases.Add($"win{version}-x86", true, $"win{version}", string.Empty, "x86", string.Empty, false);
        }

        foreach (var version in MacOSVersions)
        {
            testCases.Add($"osx.{version}-arm64", true, "osx", version, "arm64", string.Empty, false);
            testCases.Add($"osx.{version}-x64", true, "osx", version, "x64", string.Empty, false);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(RuntimeIdentifiers))]
    public static void TryParse_Returns_Correct_Value(
        string value,
        bool expectedResult,
        string? expectedOperatingSystem,
        string? expectedVersion,
        string? expectedArchitecture,
        string? expectedAdditionalQualifiers,
        bool? expectedIsPortable)
    {
        // Act
        bool actualResult = RuntimeIdentifier.TryParse(value, out var actualRid);

        // Assert
        actualResult.ShouldBe(expectedResult);

        if (expectedResult)
        {
            actualRid.ShouldNotBeNull();
            actualRid.OperatingSystem.ShouldBe(expectedOperatingSystem);
            actualRid.Version.ShouldBe(expectedVersion);
            actualRid.Architecture.ShouldBe(expectedArchitecture);
            actualRid.AdditionalQualifiers.ShouldBe(expectedAdditionalQualifiers);
            actualRid.IsPortable.ShouldBe(expectedIsPortable.GetValueOrDefault());
            actualRid.ToString().ShouldBe(value);
        }
        else
        {
            actualRid.ShouldBeNull();
        }
    }
}
