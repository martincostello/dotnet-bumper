// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper.Upgraders;

#pragma warning disable JSON002

public class AwsSamTemplateUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Json_Template(string channel)
    {
        // Arrange
        string template =
            """
            {
              "AWSTemplateFormatVersion": "2010-09-09",
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

        string templateFile = await fixture.Project.AddFileAsync("template.json", template);

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
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Yaml_Template(string channel)
    {
        // Arrange
        string template =
            """
            AWSTemplateFormatVersion: '2010-09-09'
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

        string templateFile = await fixture.Project.AddFileAsync("template.yaml", template);

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
        string template =
            """
            {
              "AWSTemplateFormatVersion": "2010-09-09",
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

        string toolsDefaults =
            """
            {
              "profile": "alexa-london-travel",
              "region": "eu-west-1",
              "configuration": "Release",
              "template": "../../my-template.json",
            }
            """;

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

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Warning);

        string actualContent = await File.ReadAllTextAsync(templateFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(template.NormalizeLineEndings().Trim());
    }

    [Theory]
    [InlineData("7.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Preview)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.GoLive)]
    [InlineData("9.0", DotNetReleaseType.Sts, DotNetSupportPhase.Active)]
    [InlineData("10.0", DotNetReleaseType.Lts, DotNetSupportPhase.Preview)]
    [InlineData("10.0", DotNetReleaseType.Lts, DotNetSupportPhase.GoLive)]
    public async Task UpgradeAsync_Warns_If_Channel_Unsupported_Yaml(
        string channel,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase)
    {
        // Arrange
        string template =
            """
            AWSTemplateFormatVersion: '2010-09-09'
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

        string toolsDefaults =
            """
            {
              "profile": "alexa-london-travel",
              "region": "eu-west-1",
              "configuration": "Release",
              "template": "my-template.yml",
            }
            """;

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

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Warning);

        string actualContent = await File.ReadAllTextAsync(templateFile);
        actualContent.NormalizeLineEndings().Trim().ShouldBe(template.NormalizeLineEndings().Trim());
    }

    [Theory]
    [InlineData("Not JSON")]
    [InlineData("[]")]
    [InlineData("[]]")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData("{\"framework\":1}")]
    [InlineData("{\"framework\":true}")]
    [InlineData("{\"framework\":\"bar\"}")]
    [InlineData("{\"framework\":{}}")]
    [InlineData("{\"framework\":[]}")]
    [InlineData("{\"function-runtime\":1}")]
    [InlineData("{\"function-runtime\":true}")]
    [InlineData("{\"function-runtime\":\"bar\"}")]
    [InlineData("{\"function-runtime\":{}}")]
    [InlineData("{\"function-runtime\":[]}")]
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

    [Fact]
    public async Task UpgradeAsync_Handles_Json_That_Is_Not_An_Aws_Template()
    {
        // Arrange
        string invalidYaml =
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
        await fixture.Project.AddFileAsync("template.json", invalidYaml);

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
    public async Task UpgradeAsync_Handles_Yaml_That_Is_Not_An_Aws_Template()
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
        await fixture.Project.AddFileAsync("template.yaml", invalidYaml);

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
        string invalidYaml =
            """
            AWSTemplateFormatVersion: '2010-09-09'
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
    public async Task UpgradeAsync_Ignores_Json_That_Unknown_Template_Version()
    {
        // Arrange
        string invalidYaml =
            """
            {
              "AWSTemplateFormatVersion": "2024-04-01",
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
        await fixture.Project.AddFileAsync("template.json", invalidYaml);

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
    public async Task UpgradeAsync_Ignores_Yaml_That_Unknown_Template_Version()
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
