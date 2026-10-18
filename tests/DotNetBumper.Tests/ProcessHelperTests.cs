// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.DotNetBumper;

public static class ProcessHelperTests
{
    [Fact]
    public static async Task RunAsync_Captures_Output_From_Process()
    {
        // Arrange
        var startInfo = CreateStartInfo("echo hello", "echo hello");

        // Act
        var actual = await ProcessHelper.RunAsync(startInfo, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Success.ShouldBeTrue();
        actual.ExitCode.ShouldBe(0);
        actual.StandardOutput.Trim().ShouldBe("hello");
        actual.StandardError.ShouldBeEmpty();
    }

    [Fact]
    public static async Task RunAsync_Captures_Output_From_Process_That_Fails()
    {
        // Arrange
        var startInfo = CreateStartInfo("echo oops 1>&2& exit 2", "echo oops 1>&2; exit 2");

        // Act
        var actual = await ProcessHelper.RunAsync(startInfo, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Success.ShouldBeFalse();
        actual.ExitCode.ShouldBe(2);
        actual.StandardOutput.ShouldBeEmpty();
        actual.StandardError.Trim().ShouldBe("oops");
    }

    [Fact]
    public static async Task RunAsync_Captures_Output_When_Streams_Are_Held_Open_After_Process_Exits()
    {
        // Arrange - Run a process that writes to both streams and then exits while a child process it
        // started, which inherited the redirected streams and so is keeping them open, is still running.
        var startInfo = CreateStartInfo(
            "echo hello& echo world 1>&2& start /b ping -n 4 127.0.0.1 >nul",
            "echo hello; echo world 1>&2; sleep 3 & exit 0");

        var drainTimeout = TimeSpan.FromSeconds(1);

        // Act
        var actual = await ProcessHelper.RunAsync(startInfo, drainTimeout, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Success.ShouldBeTrue();
        actual.ExitCode.ShouldBe(0);
        actual.StandardOutput.Trim().ShouldBe("hello");
        actual.StandardError.Trim().ShouldBe("world");
    }

    [Fact]
    public static async Task RunAsync_Throws_If_Cancelled()
    {
        // Arrange
        var startInfo = CreateStartInfo("ping -n 30 127.0.0.1 >nul", "sleep 30");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act and Assert
        await Should.ThrowAsync<OperationCanceledException>(() => ProcessHelper.RunAsync(startInfo, cts.Token));
    }

    private static ProcessStartInfo CreateStartInfo(string windowsCommand, string unixCommand)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", ["/c", windowsCommand])
            : new ProcessStartInfo("/bin/sh", ["-c", unixCommand]);

        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;

        return startInfo;
    }
}
