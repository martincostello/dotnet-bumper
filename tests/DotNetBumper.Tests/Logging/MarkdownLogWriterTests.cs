// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

public static class MarkdownLogWriterTests
{
    [Fact]
    public static async Task WriteAsync_Generates_Log_File_When_Successful()
    {
        // Arrange
        var context = new BumperLogContext()
        {
            DotNetSdkVersion = "8.0.201",
            StartedAt = new(2024, 02, 27, 12, 34, 56, TimeSpan.Zero),
            FinishedAt = new(2024, 02, 27, 12, 35, 43, TimeSpan.Zero),
            Result = nameof(ProcessingResult.Success),
        };

        var path = Path.GetTempFileName();
        var target = new MarkdownLogWriter(path);

        // Act
        await target.WriteAsync(context, CancellationToken.None);

        // Assert
        File.Exists(path).ShouldBeTrue();

        var contents = await File.ReadAllTextAsync(path);
        contents.Length.ShouldBeGreaterThan(0);

        contents.ShouldContain("Project upgraded to .NET SDK `8.0.201`.");
    }

    [Fact]
    public static async Task WriteAsync_Generates_Log_File_When_No_Upgrade()
    {
        // Arrange
        var context = new BumperLogContext()
        {
            DotNetSdkVersion = "8.0.201",
            StartedAt = new(2024, 02, 27, 12, 34, 56, TimeSpan.Zero),
            FinishedAt = new(2024, 02, 27, 12, 35, 43, TimeSpan.Zero),
            Result = nameof(ProcessingResult.None),
        };

        var path = Path.GetTempFileName();
        var target = new MarkdownLogWriter(path);

        // Act
        await target.WriteAsync(context, CancellationToken.None);

        // Assert
        File.Exists(path).ShouldBeTrue();

        var contents = await File.ReadAllTextAsync(path);
        contents.Length.ShouldBeGreaterThan(0);

        contents.ShouldContain("The project upgrade did not result in any changes being made.");
    }
}
