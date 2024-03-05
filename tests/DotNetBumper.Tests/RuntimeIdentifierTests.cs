// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class RuntimeIdentifierTests
{
    private static readonly string[] MacOSVersions = ["10.10", "10.11", "10.12", "10.13", "10.14", "10.15", "10.16", "11.0", "12", "13"];
    private static readonly string[] WindowsVersions = ["7", "8", "81", "10"];

    public static TheoryData<string, bool, string?, string?, string?, string?> RuntimeIdentifiers()
    {
        var testCases = new TheoryData<string, bool, string?, string?, string?, string?>
        {
            { string.Empty, false, null, null, null, null },
            { " ", false, null, null, null, null },
            { ";", false, null, null, null, null },
            { ";;", false, null, null, null, null },
            { "multi-targeting", false, null, null, null, null },
            { "--configuration", false, null, null, null, null },
            { "--framework", false, null, null, null, null },
            { "--runtime", false, null, null, null, null },
            { "android-arm64", true, "android", string.Empty, "arm64", string.Empty },
            { "ios-arm64", true, "ios", string.Empty, "arm64", string.Empty },
            { "linux-arm", true, "linux", string.Empty, "arm", string.Empty },
            { "linux-arm64", true, "linux", string.Empty, "arm64", string.Empty },
            { "linux-musl-x64", true, "linux-musl", string.Empty, "x64", string.Empty },
            { "linux-musl-arm64", true, "linux-musl", string.Empty, "arm64", string.Empty },
            { "linux-x64", true, "linux", string.Empty, "x64", string.Empty },
            { "osx-arm64", true, "osx", string.Empty, "arm64", string.Empty },
            { "osx-x64", true, "osx", string.Empty, "x64", string.Empty },
            { "win-arm64", true, "win", string.Empty, "arm64", string.Empty },
            { "win-x64", true, "win", string.Empty, "x64", string.Empty },
            { "win-x86", true, "win", string.Empty, "x86", string.Empty },
        };

        foreach (var version in WindowsVersions)
        {
            testCases.Add($"win{version}-arm", true, $"win{version}", string.Empty, "arm", string.Empty);
            testCases.Add($"win{version}-arm64", true, $"win{version}", string.Empty, "arm64", string.Empty);
            testCases.Add($"win{version}-x64", true, $"win{version}", string.Empty, "x64", string.Empty);
            testCases.Add($"win{version}-x86", true, $"win{version}", string.Empty, "x86", string.Empty);
        }

        foreach (var version in MacOSVersions)
        {
            testCases.Add($"osx.{version}-arm64", true, "osx", version, "arm64", string.Empty);
            testCases.Add($"osx.{version}-x64", true, "osx", version, "x64", string.Empty);
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
        string? expectedAdditionalQualifiers)
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
            actualRid.ToString().ShouldBe(value);
        }
        else
        {
            actualRid.ShouldBeNull();
        }
    }
}
