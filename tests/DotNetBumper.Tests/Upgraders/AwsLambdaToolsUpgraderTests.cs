// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper.Upgraders;

public class AwsLambdaToolsUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Properties(string channel)
    {
        // Arrange
        var configuration = new JsonObject()
        {
            ["profile"] = "alexa-london-travel",
            ["region"] = "eu-west-1",
            ["configuration"] = "Release",
            ["framework"] = "net6.0",
            ["function-architecture"] = "arm64",
            ["function-handler"] = "MyApplication",
            ["function-memory-size"] = 192,
            ["function-runtime"] = "dotnet6",
            ["function-timeout"] = 10,
        };

        using var fixture = new UpgraderFixture(outputHelper);

        var lambdaDefaultsFile = await fixture.Project.AddFileAsync(
            "aws-lambda-tools-defaults.json",
            configuration.PrettyPrint());

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

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
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active)]
    [InlineData("7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Eol)]
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
        var configuration = new JsonObject()
        {
            ["profile"] = "alexa-london-travel",
            ["region"] = "eu-west-1",
            ["configuration"] = "Release",
            ["framework"] = "net6.0",
            ["function-architecture"] = "arm64",
            ["function-handler"] = "MyApplication",
            ["function-memory-size"] = 192,
            ["function-runtime"] = "dotnet6",
            ["function-timeout"] = 10,
        };

        var fileContents = configuration.PrettyPrint();

        using var fixture = new UpgraderFixture(outputHelper);

        var lambdaDefaultsFile = await fixture.Project.AddFileAsync(
            "aws-lambda-tools-defaults.json",
            fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = releaseType,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = supportPhase,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Warning);

        string actualContent = await File.ReadAllTextAsync(lambdaDefaultsFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(fileContents.NormalizeLineEndings().Trim());
    }

    [Theory]
    [InlineData("Not JSON", ProcessingResult.Warning)]
    [InlineData("[]", ProcessingResult.Warning)]
    [InlineData("[]]", ProcessingResult.Warning)]
    [InlineData("\"value\"", ProcessingResult.Warning)]
    [InlineData("{}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"framework\":1}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"framework\":true}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"framework\":\"bar\"}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"framework\":{}}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"framework\":[]}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":1}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":true}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":\"bar\"}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":{}}", ProcessingResult.None)]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":[]}", ProcessingResult.None)]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content, ProcessingResult expected)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", content);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(expected);
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "{",
            "  \"framework\": \"net6.0\"",
            "}",
        ];

        string[] expectedLines =
        [
            "{",
            "  \"framework\": \"net10.0\"",
            "}",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string jsonFile = await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("10.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(jsonFile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(jsonFile);

        if (hasUtf8Bom)
        {
            actualBytes.ShouldStartWithUTF8Bom();
        }
        else
        {
            actualBytes.ShouldNotStartWithUTF8Bom();
        }

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    private static AwsLambdaToolsUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<AwsLambdaToolsUpgrader>());
    }
}
