// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NSubstitute;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

public static class IAnsiConsoleExtensionsTests
{
    public static TheoryData<bool, bool> Environments()
    {
        var testCases = new TheoryData<bool, bool>();

        foreach (bool isLocal in new[] { true, false })
        {
            testCases.Add(!isLocal, isLocal);
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public static void WriteDisclaimer_Does_Not_Throw(bool isGitHubActions, bool supportsLinks)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();
        var environment = CreateEnvironment(isGitHubActions, supportsLinks);

        // Act and Assert
        Should.NotThrow(() => console.WriteDisclaimer(environment, new(8, 0)));
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public static void WriteRuntimeNearingEndOfSupportWarning_Does_Not_Throw(bool isGitHubActions, bool supportsLinks)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();
        var environment = CreateEnvironment(isGitHubActions, supportsLinks);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(7, 0),
            EndOfLife = new(2024, 5, 14),
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new(7, 0, 100),
            SupportPhase = DotNetSupportPhase.Active,
        };

        foreach (var daysRemaining in new[] { 90, 1, 0, -1, -90 })
        {
            // Act and Assert
            Should.NotThrow(() => console.WriteRuntimeNearingEndOfSupportWarning(environment, upgrade, daysRemaining));
        }
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
    public static void WriteProgressLine_Does_Not_Throw(bool isGitHubActions, bool supportsLinks)
    {
        // Arrange
        var console = Substitute.For<IAnsiConsole>();
        var environment = CreateEnvironment(isGitHubActions, supportsLinks);

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

    private static IEnvironment CreateEnvironment(bool isGitHubActions, bool supportsLinks)
    {
        var environment = Substitute.For<IEnvironment>();

        environment.IsGitHubActions.Returns(isGitHubActions);
        environment.SupportsLinks.Returns(supportsLinks);

        return environment;
    }
}
