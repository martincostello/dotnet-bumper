// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

public class EndToEndTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("6.0.100", "--channel=7.0")]
    [InlineData("6.0.100", "--channel=8.0")]
    [InlineData("6.0.100", "--channel=9.0")]
    [InlineData("6.0.100", "--upgrade-type=latest")]
    [InlineData("6.0.100", "--upgrade-type=lts")]
    [InlineData("6.0.100", "--upgrade-type=preview")]
    public async Task Application_Does_Not_Error(string sdkVersion, params string[] args)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("global.json", $@"{{""sdk"":{{""version"":""{sdkVersion}""}}}}");

        // Act
        int status = await Program.Main([fixture.Project.DirectoryName, "--verbose", ..args]);

        // Assert
        status.ShouldBe(0);

        var transformed = await fixture.Project.GetFileAsync("global.json");
        using var globalJson = JsonDocument.Parse(transformed);

        string? actual = globalJson.RootElement
            .GetProperty("sdk")
            .GetProperty("version")
            .GetString();

        fixture.Console.WriteLine($".NET SDK version: {actual}");

        actual.ShouldNotBe(sdkVersion);
    }

    [Fact]
    public async Task Application_Validates_Project_Exists()
    {
        // Arrange
        string projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act
        int actual = await Program.Main([projectPath]);

        // Assert
        actual.ShouldBe(1);
    }
}
