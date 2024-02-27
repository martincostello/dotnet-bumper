// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MartinCostello.DotNetBumper.Logging;

public static class JsonLogWriterTests
{
    [Fact]
    public static async Task WriteAsync_Generates_Log_File()
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
        var target = new JsonLogWriter(path);

        // Act
        await target.WriteAsync(context, CancellationToken.None);

        // Assert
        File.Exists(path).ShouldBeTrue();

        var contents = await File.ReadAllTextAsync(path);
        contents.Length.ShouldBeGreaterThan(0);

        using var document = JsonDocument.Parse(contents);

        var expected = new[]
        {
            ("startedAt", "2024-02-27T12:34:56+00:00"),
            ("finishedAt", "2024-02-27T12:35:43+00:00"),
            ("result", "Success"),
            ("sdkVersion", "8.0.201"),
        };

        foreach (var (name, value) in expected)
        {
            document.RootElement.TryGetProperty(name, out var property).ShouldBeTrue();
            property.ValueKind.ShouldBe(JsonValueKind.String);
            property.GetString().ShouldBe(value);
        }
    }
}
