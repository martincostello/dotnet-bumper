// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Build.Framework;
using NSubstitute;

namespace MartinCostello.DotNetBumper;

public class BumperLoggerTests
{
    [Fact]
    public async Task BumperLogger_Logs_To_Json_File()
    {
        // Arrange
        var eventSource = Substitute.For<IEventSource>();
        var logFilePath = Path.GetTempFileName();

        var logger = new BumperLogger();

        // Act
        logger.Initialize(eventSource, logFilePath);
        logger.Shutdown();

        // Assert
        File.Exists(logFilePath).ShouldBeTrue();

        using var stream = File.OpenRead(logFilePath);
        var actual = await JsonSerializer.DeserializeAsync<BumperLog>(stream);

        actual.ShouldNotBeNull();
        actual.Entries.ShouldNotBeNull();
        actual.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task BumperLogger_Logs_Errors_And_Warnings_To_Json_File()
    {
        // Arrange
        var warning = new BuildWarningEventArgs(
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

        var eventSource = Substitute.For<IEventSource>();

        var logFilePath = Path.GetTempFileName();
        var logger = new BumperLogger();

        // Act
        logger.Initialize(eventSource, logFilePath);

        eventSource.WarningRaised += Raise.Event<BuildWarningEventHandler>(this, warning);
        eventSource.ErrorRaised += Raise.Event<BuildErrorEventHandler>(this, error);

        logger.Shutdown();

        // Assert
        File.Exists(logFilePath).ShouldBeTrue();

        using var stream = File.OpenRead(logFilePath);
        var actual = await JsonSerializer.DeserializeAsync<BumperLog>(stream);

        actual.ShouldNotBeNull();
        actual.Entries.ShouldNotBeNull();
        actual.Entries.Count.ShouldBe(2);

        actual.Entries[0].Type.ShouldBe("Warning");
        actual.Entries[0].Id.ShouldBe("MSB0001");
        actual.Entries[0].Message.ShouldBe("Test warning");

        actual.Entries[1].Type.ShouldBe("Error");
        actual.Entries[1].Id.ShouldBe("MSB0002");
        actual.Entries[1].Message.ShouldBe("Test error");
    }

    [Fact]
    public void BumperLogger_Logs_To_Console()
    {
        // Arrange
        var eventSource = Substitute.For<IEventSource>();
        var logFilePath = string.Empty;

        var logger = new BumperLogger();

        // Act and Assert
        Should.Throw<InvalidOperationException>(
            () => logger.Initialize(eventSource, logFilePath));
    }
}
