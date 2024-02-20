﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper.Upgraders;

public class AwsLambdaToolsUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("7.0")]
    [InlineData("8.0")]
    [InlineData("9.0")]
    public async Task UpgradeAsync_Upgrades_Properties(string channel)
    {
        // Arrange
        string fileContents =
            """
            {
              "profile": "alexa-london-travel",
              "region": "eu-west-1",
              "configuration": "Release",
              "framework": "net6.0",
              "function-architecture": "arm64",
              "function-handler": "MyApplication",
              "function-memory-size": 192,
              "function-runtime": "dotnet6",
              "function-timeout": 10
            }
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string lambdaDefaultsFile = await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<AwsLambdaToolsUpgrader>();
        var target = new AwsLambdaToolsUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.Success);

        string actualContent = await File.ReadAllTextAsync(lambdaDefaultsFile);

        var defaults = JsonDocument.Parse(actualContent);
        defaults.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);

        defaults.RootElement.TryGetProperty("framework", out var framework).ShouldBeTrue();
        framework.ValueKind.ShouldBe(JsonValueKind.String);
        framework.GetString().ShouldBe($"net{channel}");

        defaults.RootElement.TryGetProperty("function-runtime", out var runtime).ShouldBeTrue();
        runtime.ValueKind.ShouldBe(JsonValueKind.String);
        runtime.GetString().ShouldBe($"dotnet{upgrade.Channel.Major}");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.None);
    }

    [Theory]
    [InlineData("7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Preview)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.GoLive)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active)]
    [InlineData("10.0", DotNetReleaseType.Lts, DotNetSupportPhase.Preview)]
    [InlineData("10.0", DotNetReleaseType.Lts, DotNetSupportPhase.GoLive)]
    public async Task UpgradeAsync_Warns_If_Channel_Unsupported(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string fileContents =
            """
            {
              "profile": "alexa-london-travel",
              "region": "eu-west-1",
              "configuration": "Release",
              "framework": "net6.0",
              "function-architecture": "arm64",
              "function-handler": "MyApplication",
              "function-memory-size": 192,
              "function-runtime": "dotnet6",
              "function-timeout": 10
            }
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string lambdaDefaultsFile = await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = releaseType,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = supportPhase,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<AwsLambdaToolsUpgrader>();
        var target = new AwsLambdaToolsUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.Warning);

        string actualContent = await File.ReadAllTextAsync(lambdaDefaultsFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(fileContents.NormalizeLineEndings().Trim());
    }

    [Theory]
    [InlineData("Not JSON", UpgradeResult.Warning)]
    [InlineData("[]", UpgradeResult.Warning)]
    [InlineData("[]]", UpgradeResult.Warning)]
    [InlineData("\"value\"", UpgradeResult.Warning)]
    [InlineData("{}", UpgradeResult.None)]
    [InlineData("{\"framework\":1}", UpgradeResult.None)]
    [InlineData("{\"framework\":true}", UpgradeResult.None)]
    [InlineData("{\"framework\":\"bar\"}", UpgradeResult.None)]
    [InlineData("{\"framework\":{}}", UpgradeResult.None)]
    [InlineData("{\"framework\":[]}", UpgradeResult.None)]
    [InlineData("{\"function-runtime\":1}", UpgradeResult.None)]
    [InlineData("{\"function-runtime\":true}", UpgradeResult.None)]
    [InlineData("{\"function-runtime\":\"bar\"}", UpgradeResult.None)]
    [InlineData("{\"function-runtime\":{}}", UpgradeResult.None)]
    [InlineData("{\"function-runtime\":[]}", UpgradeResult.None)]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content, UpgradeResult expected)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string vsconfig = await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", content);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<AwsLambdaToolsUpgrader>();
        var target = new AwsLambdaToolsUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(expected);
    }
}
