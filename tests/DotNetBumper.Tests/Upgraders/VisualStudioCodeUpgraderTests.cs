// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MartinCostello.DotNetBumper.Upgraders;

public class VisualStudioCodeUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(launchFile, fixture.CancellationToken);
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
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("Not JSON")]
    [InlineData("[]")]
    [InlineData("[]]")]
    [InlineData("\"value\"")]
    [InlineData("{}")]
    [InlineData(/*lang=json,strict*/ "{\"configurations\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"configurations\":true}")]
    [InlineData(/*lang=json,strict*/ "{\"configurations\":\"bar\"}")]
    [InlineData(/*lang=json,strict*/ "{\"configurations\":{}}")]
    [InlineData(/*lang=json,strict*/ "{\"configurations\":[]}")]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync(".vscode/launch.json", content);

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
        ProcessingResult actual = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Updates_Tfm_Properties()
    {
        // Arrange
        // lang=json,strict
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
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Debug\\net6.0\\Project.dll",
                  "args": [
                    "--framework",
                    "net6.0"
                  ]
                }
              ]
            }
            """;

        // lang=json,strict
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
                },
                {
                  "program": "${workspaceFolder}\\src\\Project\\bin\\Debug\\net10.0\\Project.dll",
                  "args": [
                    "--framework",
                    "net10.0"
                  ]
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(filePath, fixture.CancellationToken);
        actualContent.Trim().ShouldBe(expectedContent.Trim());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom(string newLine, bool hasUtf8Bom)
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
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(filePath, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(filePath, fixture.CancellationToken);

        if (hasUtf8Bom)
        {
            actualBytes.ShouldStartWithUTF8Bom();
        }
        else
        {
            actualBytes.ShouldNotStartWithUTF8Bom();
        }

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    private static VisualStudioCodeUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<VisualStudioCodeUpgrader>());
    }
}
