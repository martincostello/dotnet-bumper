// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class EndToEndTests
{
    [Fact]
    public static async Task Application_Does_Not_Error()
    {
        // Arrange
        using var directory = new Project();

        // Act
        int actual = await Program.Main([directory.Path]);

        // Assert
        actual.ShouldBe(0);
    }

    [Fact]
    public static async Task Application_Validates_Project_Exists()
    {
        // Arrange
        string projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act
        int actual = await Program.Main([projectPath]);

        // Assert
        actual.ShouldBe(1);
    }
}
