// Copyright (c) Martin Costello, 2024. All rights reserved.
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
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

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
        actualUpdated.ShouldBe(ProcessingResult.None);
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
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Updates_Tfm_Properties()
    {
        // Arrange
        string fileContents =
            """
            {
              "configurations": [
                {
                  "program": "net6.0"
                },
                {
                  "program": "${workspaceFolder}/net6.0"
                },
                {
                  "program": "${workspaceFolder}\\net6.0"
                },
                {
                  "program": "net6.0/Project.dll"
                },
                {
                  "program": "net6.0\\Project.dll"
                },
                {
                  "program": "${workspaceFolder}/src/Project/bin/Debug/net6.0/Project.dll"
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Debug\\net6.0\\Project.dll"
                },
                {
                  "program": "./net6.0/Project.dll"
                },
                {
                  "program": ".\\net6.0\\Project.dll"
                },
                {
                  "program": "${workspaceFolder}/src/Project/bin/Debug/netcoreapp3.1/Project.dll"
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Release\\netcoreapp3.1\\Project.dll"
                }
              ]
            }
            """;

        string expectedContent =
            """
            {
              "configurations": [
                {
                  "program": "net10.0"
                },
                {
                  "program": "${workspaceFolder}/net10.0"
                },
                {
                  "program": "${workspaceFolder}\\net10.0"
                },
                {
                  "program": "net10.0/Project.dll"
                },
                {
                  "program": "net10.0\\Project.dll"
                },
                {
                  "program": "${workspaceFolder}/src/Project/bin/Debug/net10.0/Project.dll"
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Debug\\net10.0\\Project.dll"
                },
                {
                  "program": "./net10.0/Project.dll"
                },
                {
                  "program": ".\\net10.0\\Project.dll"
                },
                {
                  "program": "${workspaceFolder}/src/Project/bin/Debug/net10.0/Project.dll"
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Release\\net10.0\\Project.dll"
                }
              ]
            }
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string filePath = await fixture.Project.AddFileAsync(".vscode/launch.json", fileContents);

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

        string actualContent = await File.ReadAllTextAsync(filePath);
        actualContent.Trim().ShouldBe(expectedContent.Trim());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r", false)]
    [InlineData("\r", true)]
    [InlineData("\r\n", false)]
    [InlineData("\r\n", true)]
    public async Task UpgradeAsync_Preserves_Bom(string newLine, bool bom)
    {
        // Arrange
        string[] originalLines =
        [
            "{",
            "  \"configurations\": [",
            "    {",
            "      \"foo\": \"bin/net6.0/foo\"",
            "    }",
            "  ]",
            "}",
        ];

        string[] expectedLines =
        [
            "{",
            "  \"configurations\": [",
            "    {",
            "      \"foo\": \"bin/net10.0/foo\"",
            "    }",
            "  ]",
            "}",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(Environment.NewLine, expectedLines) + Environment.NewLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(bom);
        string filePath = await fixture.Project.AddFileAsync(".vscode/launch.json", fileContents, encoding);

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

        string actualContent = await File.ReadAllTextAsync(filePath);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(filePath);

        if (bom)
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

    private VisualStudioCodeUpgrader CreateTarget(UpgraderFixture fixture)
    {
        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<VisualStudioCodeUpgrader>();
        return new VisualStudioCodeUpgrader(fixture.Console, options, logger);
    }
}
