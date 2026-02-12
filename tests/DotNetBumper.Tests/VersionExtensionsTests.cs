// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class VersionExtensionsTests
{
    [Theory]
    [InlineData("netcoreapp1.0")]
    [InlineData("netcoreapp1.1")]
    [InlineData("netcoreapp2.0")]
    [InlineData("netcoreapp2.1")]
    [InlineData("netcoreapp2.2")]
    [InlineData("netcoreapp3.0")]
    [InlineData("netcoreapp3.1")]
    [InlineData("net5.0")]
    [InlineData("net6.0")]
    [InlineData("net7.0")]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    [InlineData("net20.0")]
    [InlineData("net99.0")]
    [InlineData("net100.0")]
    public static void IsTargetFrameworkMoniker_Is_Valid(string value)
    {
        // Act
        bool actual = value.IsTargetFrameworkMoniker();

        // Assert
        actual.ShouldBeTrue();
    }

    [Theory]
    [InlineData("dotnetcore1.0", "1.0")]
    [InlineData("dotnetcore2.0", "2.0")]
    [InlineData("dotnetcore2.1", "2.1")]
    [InlineData("dotnetcore3.1", "3.1")]
    [InlineData("dotnet5.0", "5.0")]
    [InlineData("dotnet6", "6.0")]
    [InlineData("dotnet7", "7.0")]
    [InlineData("dotnet8", "8.0")]
    [InlineData("dotnet9", "9.0")]
    [InlineData("dotnet10", "10.0")]
    [InlineData("dotnet11", "11.0")]
    [InlineData("dotnet20", "20.0")]
    [InlineData("dotnet99", "99.0")]
    [InlineData("dotnet100", "100.0")]
    public static void ToVersionFromLambdaRuntime_Returns_Expected_Result(string value, string expected)
    {
        // Act
        var actual = value.ToVersionFromLambdaRuntime();

        // Assert
        actual.ShouldBe(Version.Parse(expected));
    }

    [Theory]
    [InlineData("netcoreapp1.0", "1.0")]
    [InlineData("netcoreapp1.1", "1.1")]
    [InlineData("netcoreapp2.0", "2.0")]
    [InlineData("netcoreapp2.1", "2.1")]
    [InlineData("netcoreapp2.2", "2.2")]
    [InlineData("netcoreapp3.0", "3.0")]
    [InlineData("netcoreapp3.1", "3.1")]
    [InlineData("net5.0", "5.0")]
    [InlineData("net6.0", "6.0")]
    [InlineData("net7.0", "7.0")]
    [InlineData("net8.0", "8.0")]
    [InlineData("net9.0", "9.0")]
    [InlineData("net10.0", "10.0")]
    [InlineData("net11.0", "11.0")]
    [InlineData("net20.0", "20.0")]
    [InlineData("net99.0", "99.0")]
    [InlineData("net100.0", "100.0")]
    public static void ToVersionFromTargetFramework_Returns_Expected_Result(string value, string expected)
    {
        // Act
        var actual = value.ToVersionFromTargetFramework();

        // Assert
        actual.ShouldBe(Version.Parse(expected));
    }
}
