// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NSubstitute;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

public static class IAnsiConsoleExtensionsTests
{
    public static TheoryData<IEnvironment> Environments()
    {
        var testCases = new TheoryData<IEnvironment>();

        foreach (bool isLocal in new[] { true, false })
        {
            var environment = Substitute.For<IEnvironment>();

            environment.IsGitHubActions.Returns(!isLocal);
            environment.SupportsLinks.Returns(isLocal);

            testCases.Add(environment);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public static void WriteDisclaimer_Does_Not_Throw(IEnvironment environment)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteDisclaimer(environment, new(8, 0)));
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public static void WriteRuntimeNearingEndOfSupportWarning_Does_Not_Throw(IEnvironment environment)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        var upgrade = new UpgradeInfo()
        {
            Channel = new(7, 0),
            EndOfLife = new(2024, 5, 14),
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new(7, 0, 100),
            SupportPhase = DotNetSupportPhase.Active,
        };

        // Act and Assert
        Should.NotThrow(() => console.WriteRuntimeNearingEndOfSupportWarning(environment, upgrade, 90));
    }

    [Fact]
    public static void WriteErrorLine_Does_Not_Throw()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteErrorLine("An error."));
    }

    [Fact]
    public static void WriteExceptionLine_Does_Not_Throw()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteExceptionLine("An error.", new InvalidOperationException("An exception.")));
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public static void WriteProgressLine_Does_Not_Throw(IEnvironment environment)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteProgressLine(environment, "A progress message."));
    }

    [Fact]
    public static void WriteSuccessLine_Does_Not_Throw()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteSuccessLine("Success!"));
    }

    [Fact]
    public static void WriteWarningLine_Does_Not_Throw()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        // Act and Assert
        Should.NotThrow(() => console.WriteWarningLine("A warning."));
    }

    [Fact]
    public static void WriteUnsupportedLambdaRuntimeWarning_Does_Not_Throw()
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();

        var upgrade = new UpgradeInfo()
        {
            Channel = new(7, 0),
            EndOfLife = new(2024, 5, 14),
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new(7, 0, 100),
            SupportPhase = DotNetSupportPhase.Active,
        };

        // Act and Assert
        Should.NotThrow(() => console.WriteUnsupportedLambdaRuntimeWarning(upgrade));
    }
}
