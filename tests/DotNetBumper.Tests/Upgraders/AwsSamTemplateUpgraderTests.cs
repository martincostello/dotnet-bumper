// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper.Upgraders;

public class AwsSamTemplateUpgraderTests(ITestOutputHelper outputHelper)
{
    public static string AwsLambdaToolsDefaults(string template) =>
        new JsonObject()
        {
            ["profile"] = "my-profile",
            ["region"] = "eu-west-1",
            ["configuration"] = "Release",
            ["template"] = template,
        }.PrettyPrint();

    public static string JsonTemplate(string runtime = "dotnet6") =>
        new JsonObject()
        {
            ["AWSTemplateFormatVersion"] = "2010-09-09",
            ["Globals"] = new JsonObject()
            {
                ["Function"] = new JsonObject()
                {
                    ["Runtime"] = runtime,
                },
            },
            ["Resources"] = new JsonObject()
            {
                ["MyFunction"] = new JsonObject()
                {
                    ["Type"] = "AWS::Serverless::Function",
                    ["Properties"] = new JsonObject()
                    {
                        ["Handler"] = "MyFunction",
                        ["Runtime"] = runtime,
                    },
                },
            },
        }.PrettyPrint();

    public static string YamlTemplate(string runtime = "dotnet6") =>
        $"""
         AWSTemplateFormatVersion: '2010-09-09'
         Globals:
           Function:
             Runtime: {runtime}
         Resources:
           MyFunction:
             Type: AWS::Serverless::Function
             Properties:
               Handler: MyFunction
               Runtime: {runtime}
         """;

    public static TheoryData<string, string> ChannelsForJson()
    {
        var testCases = new TheoryData<string, string>();
        var channels = new DotNetChannelTestData();

        foreach (string channel in channels)
        {
            testCases.Add(channel, "template.json");
            testCases.Add(channel, "serverless.template");
        }

        return testCases;
    }

    public static TheoryData<string, string> ChannelsForYaml()
    {
        var testCases = new TheoryData<string, string>();
        var channels = new DotNetChannelTestData();

        foreach (string channel in channels)
        {
            testCases.Add(channel, "template.yaml");
            testCases.Add(channel, "template.yml");
            testCases.Add(channel, "serverless.template");
        }

        return testCases;
    }

    public static TheoryData<string, DotNetReleaseType, DotNetSupportPhase> UnsupportedLambdaRuntimes()
    {
        return new()
        {
            { "7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active },
            { "7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Eol },
            { "9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Preview },
            { "9.0", DotNetReleaseType.Sts, DotNetSupportPhase.GoLive },
            { "9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active },
            { "10.0", DotNetReleaseType.Lts, DotNetSupportPhase.Preview },
            { "10.0", DotNetReleaseType.Lts, DotNetSupportPhase.GoLive },
        };
    }

    [Theory]
    [MemberData(nameof(ChannelsForJson))]
    public async Task UpgradeAsync_Upgrades_Json_Template(string channel, string fileName)
    {
        // Arrange
        string template = JsonTemplate();

        using var fixture = new UpgraderFixture(outputHelper);

        string templateFile = await fixture.Project.AddFileAsync(fileName, template);

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

        string actualContent = await File.ReadAllTextAsync(templateFile);

        var samTemplate = JsonDocument.Parse(actualContent);
        samTemplate.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);

        samTemplate.RootElement.TryGetProperty("Globals", out var node).ShouldBeTrue();
        node.TryGetProperty("Function", out node).ShouldBeTrue();
        node.TryGetProperty("Runtime", out node).ShouldBeTrue();
        node.ValueKind.ShouldBe(JsonValueKind.String);
        node.GetString().ShouldBe($"dotnet{upgrade.Channel.Major}");

        samTemplate.RootElement.TryGetProperty("Resources", out node).ShouldBeTrue();
        node.TryGetProperty("MyFunction", out node).ShouldBeTrue();
        node.TryGetProperty("Properties", out node).ShouldBeTrue();
        node.TryGetProperty("Runtime", out node).ShouldBeTrue();
        node.ValueKind.ShouldBe(JsonValueKind.String);
        node.GetString().ShouldBe($"dotnet{upgrade.Channel.Major}");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [MemberData(nameof(ChannelsForYaml))]
    public async Task UpgradeAsync_Upgrades_Yaml_Template(string channel, string fileName)
    {
        // Arrange
        string template = YamlTemplate();

        using var fixture = new UpgraderFixture(outputHelper);

        string templateFile = await fixture.Project.AddFileAsync(fileName, template);

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

        string actualContent = await File.ReadAllTextAsync(templateFile);

        actualContent.Split(Environment.NewLine)
                     .Count((p) => p.Contains($"dotnet{upgrade.Channel.Major}", StringComparison.Ordinal))
                     .ShouldBe(2);

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
    public async Task UpgradeAsync_Warns_If_Channel_Unsupported_Json(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string template = JsonTemplate();
        string toolsDefaults = AwsLambdaToolsDefaults("../../my-template.json");

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("src/Project/aws-lambda-tools-defaults.json", toolsDefaults);
        string templateFile = await fixture.Project.AddFileAsync("my-template.json", template);

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

        string actualContent = await File.ReadAllTextAsync(templateFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(template.NormalizeLineEndings().Trim());
    }

    [Theory]
    [MemberData(nameof(UnsupportedLambdaRuntimes))]
    public async Task UpgradeAsync_Warns_If_Channel_Unsupported_Yaml(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string template = YamlTemplate();
        string toolsDefaults = AwsLambdaToolsDefaults("my-template.yml");

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("src/Project/aws-lambda-tools-defaults.json", toolsDefaults);
        string templateFile = await fixture.Project.AddFileAsync("my-template.yml", template);

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

        string actualContent = await File.ReadAllTextAsync(templateFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(template.NormalizeLineEndings().Trim());
    }

    [Theory]
    [MemberData(nameof(UnsupportedLambdaRuntimes))]
    public async Task UpgradeAsync_Does_Not_Warn_If_Channel_Unsupported_But_Not_DotNet_Managed_Runtime_Json(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string template = JsonTemplate("nodejs20.x");
        string toolsDefaults = AwsLambdaToolsDefaults("my-template.json");

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("src/Project/aws-lambda-tools-defaults.json", toolsDefaults);
        await fixture.Project.AddFileAsync("my-template.json", template);

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
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [MemberData(nameof(UnsupportedLambdaRuntimes))]
    public async Task UpgradeAsync_Does_Not_Warn_If_Channel_Unsupported_But_Not_DotNet_Managed_Runtime_Yaml(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string template = YamlTemplate("provided.al2023");
        string toolsDefaults = AwsLambdaToolsDefaults("my-template.yml");

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("src/Project/aws-lambda-tools-defaults.json", toolsDefaults);
        await fixture.Project.AddFileAsync("my-template.yml", template);

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
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("Not JSON")]
    [InlineData("[]")]
    [InlineData("[]]")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData(/*lang=json,strict*/ "{\"framework\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"framework\":true}")]
    [InlineData(/*lang=json,strict*/ "{\"framework\":\"bar\"}")]
    [InlineData(/*lang=json,strict*/ "{\"framework\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"framework\":[]}")]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":true}")]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":\"bar\"}")]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"function-runtime\":[]}")]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("template.json", content);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpgradeAsync_Handles_Invalid_Yaml()
    {
        // Arrange
        string invalidYaml =
            """
            AWSTemplateFormatVersion: '2010-09-09'
            foo: bar
            baz
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("template.yml", invalidYaml);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("template.json")]
    [InlineData("serverless.template")]
    public async Task UpgradeAsync_Handles_Json_That_Is_Not_An_Aws_Template(string fileName)
    {
        // Arrange
        // lang=json,strict
        string invalidJson =
            """
            {
              "Globals": {
                "Function": {
                  "Runtime": "dotnet6"
                }
              },
              "Resources": {
                "MyFunction": {
                  "Type": "AWS::Serverless::Function",
                  "Properties": {
                    "Handler": "MyFunction",
                    "Runtime": "dotnet6"
                  }
                }
              }
            }
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync(fileName, invalidJson);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("template.yaml")]
    [InlineData("template.yml")]
    [InlineData("serverless.template")]
    public async Task UpgradeAsync_Handles_Yaml_That_Is_Not_An_Aws_Template(string fileName)
    {
        // Arrange
        string invalidYaml =
            """
            Globals:
              Function:
                Runtime: dotnet6
            Resources:
              MyFunction:
                Type: AWS::Serverless::Function
                Properties:
                  Handler: MyFunction
                  Runtime: dotnet6
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync(fileName, invalidYaml);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpgradeAsync_Ignores_Yaml_That_Is_In_Aws_Sam_Build_Directory()
    {
        // Arrange
        string invalidYaml = YamlTemplate();

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync(".aws-sam/template.yaml", invalidYaml);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpgradeAsync_Ignores_Json_With_Unknown_Template_Version()
    {
        // Arrange
        var invalidJson = new JsonObject()
        {
            ["AWSTemplateFormatVersion"] = "2024-04-01",
            ["Globals"] = new JsonObject()
            {
                ["Function"] = new JsonObject()
                {
                    ["Runtime"] = "dotnet6",
                },
            },
            ["Resources"] = new JsonObject()
            {
                ["MyFunction"] = new JsonObject()
                {
                    ["Type"] = "AWS::Serverless::Function",
                    ["Properties"] = new JsonObject()
                    {
                        ["Handler"] = "MyFunction",
                        ["Runtime"] = "dotnet6",
                    },
                },
            },
        }.PrettyPrint();

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("template.json", invalidJson);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpgradeAsync_Ignores_Yaml_With_Unknown_Template_Version()
    {
        // Arrange
        string invalidYaml =
            """
            AWSTemplateFormatVersion: '2024-04-01'
            Globals:
              Function:
                Runtime: dotnet6
            Resources:
              MyFunction:
                Type: AWS::Serverless::Function
                Properties:
                  Handler: MyFunction
                  Runtime: dotnet6
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("template.yml", invalidYaml);

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
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom_For_Json(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "{",
            "  \"AWSTemplateFormatVersion\": \"2010-09-09\",",
            "  \"Resources\": {",
            "    \"MyFunction\": {",
            "      \"Handler\": \"MyFunction\",",
            "      \"Runtime\": \"dotnet6\"",
            "    }",
            "  }",
            "}",
        ];

        string[] expectedLines =
        [
            "{",
            "  \"AWSTemplateFormatVersion\": \"2010-09-09\",",
            "  \"Resources\": {",
            "    \"MyFunction\": {",
            "      \"Handler\": \"MyFunction\",",
            "      \"Runtime\": \"dotnet8\"",
            "    }",
            "  }",
            "}",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string jsonFile = await fixture.Project.AddFileAsync("template.json", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("8.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.100"),
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

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom_For_Yaml(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "AWSTemplateFormatVersion: '2010-09-09'",
            "Globals:",
            "  Function:",
            "    Runtime: dotnet6",
            "Resources:",
            "  MyFunction:",
            "    Type: AWS::Serverless::Function",
            "    Properties:",
            "      Handler: MyFunction",
            "      Runtime: dotnet6",
            "  MyOtherFunction:",
            "    Type: AWS::Serverless::Function",
            "    Properties:",
            "      Handler: MyOtherFunction",
            "      Runtime: provided.al2023",
        ];

        string[] expectedLines =
        [
            "AWSTemplateFormatVersion: '2010-09-09'",
            "Globals:",
            "  Function:",
            "    Runtime: dotnet8",
            "Resources:",
            "  MyFunction:",
            "    Type: AWS::Serverless::Function",
            "    Properties:",
            "      Handler: MyFunction",
            "      Runtime: dotnet8",
            "  MyOtherFunction:",
            "    Type: AWS::Serverless::Function",
            "    Properties:",
            "      Handler: MyOtherFunction",
            "      Runtime: provided.al2023",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string jsonFile = await fixture.Project.AddFileAsync("template.yml", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("8.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.100"),
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

    private static AwsSamTemplateUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<AwsSamTemplateUpgrader>());
    }
}
