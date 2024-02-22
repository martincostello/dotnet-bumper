// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper.Upgraders;

public class VisualStudioComponentUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("7.0")]
    [InlineData("8.0")]
    [InlineData("9.0")]
    public async Task UpgradeAsync_Upgrades_Component_Configuration(string channel)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string serverlessFile = await fixture.Project.AddVisualStudioConfigurationAsync("6.0");

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

        string actualContent = await File.ReadAllTextAsync(serverlessFile);
        actualContent.ShouldContain($"\"Microsoft.NetCore.Component.Runtime.{channel}\"");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Upgrades_Component_Configuration_With_Multiple_Runtimes()
    {
        // Arrange
        string content =
            $$"""
              {
                "version": "1.0",
                "components": [
                  "Component.GitHub.VisualStudio",
                  "Microsoft.NetCore.Component.Runtime.6.0",
                  "Microsoft.NetCore.Component.Runtime.7.0",
                  "Microsoft.NetCore.Component.SDK",
                  "Microsoft.VisualStudio.Component.CoreEditor",
                  "Microsoft.VisualStudio.Component.Git",
                  "Microsoft.VisualStudio.Workload.CoreEditor"
                ]
              }
              """;

        string expectedContent =
            $$"""
              {
                "version": "1.0",
                "components": [
                  "Component.GitHub.VisualStudio",
                  "Microsoft.NetCore.Component.Runtime.6.0",
                  "Microsoft.NetCore.Component.Runtime.7.0",
                  "Microsoft.NetCore.Component.Runtime.8.0",
                  "Microsoft.NetCore.Component.SDK",
                  "Microsoft.VisualStudio.Component.CoreEditor",
                  "Microsoft.VisualStudio.Component.Git",
                  "Microsoft.VisualStudio.Workload.CoreEditor"
                ]
              }
              """;

        using var fixture = new UpgraderFixture(outputHelper);

        string vsconfig = await fixture.Project.AddFileAsync(".vsconfig", content);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
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

        string actualContent = await File.ReadAllTextAsync(vsconfig);
        actualContent.NormalizeLineEndings().TrimEnd().ShouldBe(expectedContent.NormalizeLineEndings().TrimEnd());

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
    [InlineData("{\"foo\":\"bar\"}")]
    [InlineData("{\"components\":{}}")]
    public async Task UpgradeAsync_Handles_Invalid_Json(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string vsconfig = await fixture.Project.AddFileAsync(".vsconfig", content);

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

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "{",
            "  \"components\": [",
            "    \"Microsoft.NetCore.Component.Runtime.6.0\"",
            "  ]",
            "}",
        ];

        string[] expectedLines =
        [
            "{",
            "  \"components\": [",
            "    \"Microsoft.NetCore.Component.Runtime.10.0\"",
            "  ]",
            "}",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(Environment.NewLine, expectedLines) + Environment.NewLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string dockerfile = await fixture.Project.AddFileAsync(".vsconfig", fileContents, encoding);

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

        string actualContent = await File.ReadAllTextAsync(dockerfile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(dockerfile);

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

    private VisualStudioComponentUpgrader CreateTarget(UpgraderFixture fixture)
    {
        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<VisualStudioComponentUpgrader>();
        return new VisualStudioComponentUpgrader(fixture.Console, options, logger);
    }
}
