// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class RuntimeIdentifierHelpersTests
{
    public static TheoryData<string, bool, string?> RuntimeIdentifiers()
    {
        var testCases = new TheoryData<string, bool, string?>
        {
            { string.Empty, false, null },
            { " ", false, null },
            { ";", false, null },
            { ";;", false, null },
            { "alpine-x64", true, "linux-musl-x64" },
            { "android-arm64", false, null },
            { "arch-x64", true, "linux-x64" },
            { "browser-wasm", false, null },
            { "centos-x64", true, "linux-x64" },
            { "debian-x64", true, "linux-x64" },
            { "fedora-x64", true, "linux-x64" },
            { "gentoo-x64", true, "linux-x64" },
            { "haiku-x64", false, null },
            { "illumos-x64", false, null },
            { "ios-arm64", false, null },
            { "iossimulator-arm64", false, null },
            { "linux-arm", false, null },
            { "linux-arm64", false, null },
            { "linux-musl-x64", false, null },
            { "linux-musl-arm64", false, null },
            { "linux-x64", false, null },
            { "linuxmint-x64", true, "linux-x64" },
            { "maccatalyst-arm64", false, null },
            { "miraclelinux-x64", true, "linux-x64" },
            { "ol-x64", true, "linux-x64" },
            { "omnios-x64", true, "illumos-x64" },
            { "openindiana-x64", true, "illumos-x64" },
            { "opensuse-x64", true, "linux-x64" },
            { "osx-arm64", false, null },
            { "osx-x64", false, null },
            { "rhel-x64", true, "linux-x64" },
            { "rocky-x64", true, "linux-x64" },
            { "sles-x64", true, "linux-x64" },
            { "solaris-x64", false, null },
            { "smartos-x64", true, "illumos-x64" },
            { "tizen-x64", true, "linux-x64" },
            { "tvos-arm64", false, null },
            { "tvossimulator-arm64", false, null },
            { "ubuntu-x64", true, "linux-x64" },
            { "ubuntu.16.04-x64", true, "linux-x64" },
            { "unix-x64", false, null },
            { "wasi-wasm", false, null },
            { "win-arm64", false, null },
            { "win-x64", false, null },
            { "win-x86", false, null },
            { "win10-x64;osx.10.11-x64;ubuntu.16.04-x64", true, "win-x64;osx-x64;linux-x64" },
        };

        foreach (var version in RuntimeIdentifierTests.WindowsVersions)
        {
            testCases.Add($"win{version}-aot", true, "win-aot");
            testCases.Add($"win{version}-arm", true, "win-arm");
            testCases.Add($"win{version}-arm64", true, "win-arm64");
            testCases.Add($"win{version}-x64", true, "win-x64");
            testCases.Add($"win{version}-x86", true, "win-x86");
            testCases.Add($";win{version}-x86;", true, ";win-x86;");
            testCases.Add($"linux-x64;osx-x64;win{version}-x64", true, "linux-x64;osx-x64;win-x64");
        }

        foreach (var version in RuntimeIdentifierTests.MacOSVersions)
        {
            testCases.Add($"osx.{version}-arm64", true, "osx-arm64");
            testCases.Add($"osx.{version}-x64", true, "osx-x64");
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(RuntimeIdentifiers))]
    public static void TryUpdateRid_Returns_Correct_Value(string value, bool expectedResult, string? expectedValue)
    {
        // Act
        bool actualResult = RuntimeIdentifierHelpers.TryUpdateRid(value, out var actualUpdated);

        // Assert
        actualResult.ShouldBe(expectedResult);
        actualUpdated.ShouldBe(expectedValue);
    }

    public static TheoryData<string, bool, string?> RuntimeIdentifierPaths()
    {
        var testCases = new TheoryData<string, bool, string?>
        {
            { string.Empty, false, null },
            { " ", false, null },
            { ";", false, null },
            { ";;", false, null },
            { "android-arm64", false, null },
            { "ios-arm64", false, null },
            { "linux-arm", false, null },
            { "linux-arm64", false, null },
            { "linux-musl-x64", false, null },
            { "linux-musl-arm64", false, null },
            { "linux-x64", false, null },
            { "osx-arm64", false, null },
            { "osx-x64", false, null },
            { "win-arm64", false, null },
            { "win-x64", false, null },
            { "win-x86", false, null },
        };

        foreach (var version in RuntimeIdentifierTests.WindowsVersions)
        {
            testCases.Add($"bin\\Release\\win{version}-aot", true, "bin\\Release\\win-aot");
            testCases.Add($"bin\\Release\\win{version}-arm", true, "bin\\Release\\win-arm");
            testCases.Add($"bin\\Release\\win{version}-arm64", true, "bin\\Release\\win-arm64");
            testCases.Add($"bin\\Release\\win{version}-x64", true, "bin\\Release\\win-x64");
            testCases.Add($"bin\\Release\\win{version}-x86", true, "bin\\Release\\win-x86");
        }

        foreach (var version in RuntimeIdentifierTests.MacOSVersions)
        {
            testCases.Add($"bin/Release/osx.{version}-arm64", true, "bin/Release/osx-arm64");
            testCases.Add($"bin/Release/osx.{version}-x64", true, "bin/Release/osx-x64");
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(RuntimeIdentifierPaths))]
    public static void TryUpdateRidInPath_Returns_Correct_Value(string value, bool expectedResult, string? expectedValue)
    {
        // Act
        bool actualResult = RuntimeIdentifierHelpers.TryUpdateRidInPath(value, out var actualUpdated);

        // Assert
        actualResult.ShouldBe(expectedResult);
        actualUpdated.ShouldBe(expectedValue);
    }
}
