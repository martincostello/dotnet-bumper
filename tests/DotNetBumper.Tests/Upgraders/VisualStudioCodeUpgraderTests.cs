﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgraders;

public class VisualStudioCodeUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("7.0")]
    [InlineData("8.0")]
    [InlineData("9.0")]
    public async Task UpgradeAsync_Upgrades_Launch_Path(string channel)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string launchFile = await fixture.Project.AddVisualStudioCodeLaunchConfigurationsAsync("6.0");

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<VisualStudioCodeUpgrader>();
        var target = new VisualStudioCodeUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.Success);

        string actualContent = await File.ReadAllTextAsync(launchFile);
        var launch = JsonDocument.Parse(actualContent);

        launch.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);

        launch.RootElement.TryGetProperty("configurations", out var property).ShouldBeTrue();
        property.ValueKind.ShouldBe(JsonValueKind.Array);

        var configurations = property.EnumerateArray();
        configurations.Count().ShouldBe(1);

        var configuration = configurations.First();
        configuration.TryGetProperty("program", out property).ShouldBeTrue();

        property.ValueKind.ShouldBe(JsonValueKind.String);
        property.GetString().ShouldStartWith("${workspaceFolder}/src/Project/bin/Debug/net");
        property.GetString().ShouldBe($"${{workspaceFolder}}/src/Project/bin/Debug/net{channel}/Project.dll");

        configuration.TryGetProperty("serverReadyAction", out property).ShouldBeTrue();
        property.TryGetProperty("pattern", out property).ShouldBeTrue();
        property.ValueKind.ShouldBe(JsonValueKind.String);
        property.GetString().ShouldBe("\\bNow listening on:\\s+(https?://\\S+)");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.None);
    }

    [Theory]
    [InlineData("Not JSON")]
    [InlineData("[]")]
    [InlineData("[]]")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData("{\"configurations\":1}")]
    [InlineData("{\"configurations\":true}")]
    [InlineData("{\"configurations\":\"bar\"}")]
    [InlineData("{\"configurations\":{}}")]
    [InlineData("{\"configurations\":[]}")]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string vsconfig = await fixture.Project.AddFileAsync(".vscode/launch.json", content);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<VisualStudioCodeUpgrader>();
        var target = new VisualStudioCodeUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(UpgradeResult.None);
    }
}
