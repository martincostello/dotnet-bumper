// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class RuntimeIdentifierTests
{
    private static readonly string[] MacOSVersions = ["10.10", "10.11", "10.12", "10.13", "10.14", "10.15", "10.16", "11.0", "12", "13"];
    private static readonly string[] WindowsVersions = ["7", "8", "81", "10"];

    public static TheoryData<string, bool, string?> RuntimeIdentifiers()
    {
        var testCases = new TheoryData<string, bool, string?>
        {
            { string.Empty, false, null },
            { " ", false, null },
            { ";", false, null },
            { ";;", false, null },
            { "multi-targeting", false, null },
            { "--configuration", false, null },
            { "--framework", false, null },
            { "--runtime", false, null },
            { "android-arm64", true, "android-arm64" },
            { "ios-arm64", true, "ios-arm64" },
            { "linux-arm", true, "linux-arm" },
            { "linux-arm64", true, "linux-arm64" },
            { "linux-musl-x64", true, "linux-musl-x64" },
            { "linux-musl-arm64", true, "linux-musl-arm64" },
            { "linux-x64", true, "linux-x64" },
            { "osx-arm64", true, "osx-arm64" },
            { "osx-x64", true, "osx-x64" },
            { "win-arm64", true, "win-arm64" },
            { "win-x64", true, "win-x64" },
            { "win-x86", true, "win-x86" },
        };

        foreach (var version in WindowsVersions)
        {
            testCases.Add($"win{version}-aot", true, $"win{version}-aot");
            testCases.Add($"win{version}-arm", true, $"win{version}-arm");
            testCases.Add($"win{version}-arm64", true, $"win{version}-arm64");
            testCases.Add($"win{version}-x64", true, $"win{version}-x64");
            testCases.Add($"win{version}-x86", true, $"win{version}-x86");
        }

        foreach (var version in MacOSVersions)
        {
            testCases.Add($"osx.{version}-arm64", true, $"osx.{version}-arm64");
            testCases.Add($"osx.{version}-x64", true, $"osx.{version}-x64");
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(RuntimeIdentifiers))]
    public static void TryParse_Returns_Correct_Value(string value, bool expectedResult, string? expectedValue)
    {
        // Act
        bool actualResult = RuntimeIdentifier.TryParse(value, out var actualRid);

        // Assert
        actualResult.ShouldBe(expectedResult);

        if (expectedResult)
        {
            actualRid.ShouldNotBeNull();
            actualRid.ToString().ShouldBe(expectedValue);
        }
        else
        {
            actualRid.ShouldBeNull();
        }
    }
}
