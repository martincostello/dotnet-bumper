// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Build.Framework;
using NSubstitute;

namespace MartinCostello.DotNetBumper;

public class BumperBuildLoggerTests
{
    [Fact]
    public async Task BumperBuildLogger_Logs_To_Json_File()
    {
        // Arrange
        var eventSource = Substitute.For<IEventSource>();
        var logFilePath = Path.GetTempFileName();

        var logger = new BumperBuildLogger();

        // Act
        logger.Initialize(eventSource, logFilePath);
        logger.Shutdown();

        // Assert
        File.Exists(logFilePath).ShouldBeTrue();

        using var stream = File.OpenRead(logFilePath);
        var actual = await JsonSerializer.DeserializeAsync<BumperBuildLog>(stream);

        actual.ShouldNotBeNull();
        actual.Entries.ShouldNotBeNull();
        actual.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task BumperLogger_Logs_Errors_And_Warnings_To_Json_File()
    {
        // Arrange
        var warning1 = new BuildWarningEventArgs(
            "Tests",
            "MSB0001",
            "Test.cs",
            1,
            1,
            1,
            1,
            "Test warning",
            "Test",
            "Test");

        var error = new BuildErrorEventArgs(
            "Tests",
            "MSB0002",
            "Test.cs",
            1,
            1,
            1,
            1,
            "Test error",
            "Test",
            "Test");

        var warning2 = new BuildWarningEventArgs(
            "Tests",
            "MSB0001",
            "Test.cs",
            2,
            2,
            2,
            2,
            "Test warning",
            "Test",
            "Test");

        var eventSource = Substitute.For<IEventSource>();

        var logFilePath = Path.GetTempFileName();
        var logger = new BumperBuildLogger();

        // Act
        logger.Initialize(eventSource, logFilePath);

        eventSource.WarningRaised += Raise.Event<BuildWarningEventHandler>(this, warning1);
        eventSource.ErrorRaised += Raise.Event<BuildErrorEventHandler>(this, error);
        eventSource.WarningRaised += Raise.Event<BuildWarningEventHandler>(this, warning2);

        logger.Shutdown();

        // Assert
        File.Exists(logFilePath).ShouldBeTrue();

        using var stream = File.OpenRead(logFilePath);
        var actual = await JsonSerializer.DeserializeAsync<BumperBuildLog>(stream);

        actual.ShouldNotBeNull();

        actual.Entries.ShouldNotBeNull();
        actual.Entries.Count.ShouldBe(3);

        actual.Entries[0].ShouldSatisfyAllConditions(
            "Entry[0] is incorrect.",
            (entry) => entry.Type.ShouldBe("Warning"),
            (entry) => entry.Id.ShouldBe("MSB0001"),
            (entry) => entry.Message.ShouldBe("Test warning"));

        actual.Entries[1].ShouldSatisfyAllConditions(
            "Entry[1] is incorrect.",
            (entry) => entry.Type.ShouldBe("Error"),
            (entry) => entry.Id.ShouldBe("MSB0002"),
            (entry) => entry.Message.ShouldBe("Test error"));

        actual.Entries[2].ShouldSatisfyAllConditions(
            "Entry[2] is incorrect.",
            (entry) => entry.Type.ShouldBe("Warning"),
            (entry) => entry.Id.ShouldBe("MSB0001"),
            (entry) => entry.Message.ShouldBe("Test warning"));

        actual.Summary.ShouldNotBeNull();
        actual.Summary.Count.ShouldBe(2);

        actual.Summary.ShouldSatisfyAllConditions(
            "Summary is incorrect",
            (summary) => summary.ShouldContainKey("Warning"),
            (summary) => summary["Warning"].Count.ShouldBe(1));

        actual.Summary.ShouldSatisfyAllConditions(
            "Summary is incorrect",
            (summary) => summary.ShouldContainKey("Error"),
            (summary) => summary["Error"].Count.ShouldBe(1));

        actual.Summary["Warning"].ShouldContainKeyAndValue("MSB0001", 2);
        actual.Summary["Error"].ShouldContainKeyAndValue("MSB0002", 1);
    }

    [Fact]
    public void BumperBuildLogger_Initialize_Throws_If_No_Path_Specified()
    {
        // Arrange
        var eventSource = Substitute.For<IEventSource>();
        var logFilePath = string.Empty;

        var logger = new BumperBuildLogger();

        // Act and Assert
        Should.Throw<InvalidOperationException>(
            () => logger.Initialize(eventSource, logFilePath));
    }
}
