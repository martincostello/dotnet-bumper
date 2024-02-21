// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgraders;

public class GlobalJsonUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r", false)]
    [InlineData("\r", true)]
    [InlineData("\r\n", false)]
    [InlineData("\r\n", true)]
    public async Task UpgradeAsync_Preserves_Line_Endings(string newLine, bool bom)
    {
        // Arrange
        string[] originalLines =
        [
            "{",
            "  \"sdk\": {",
            "    \"version\": \"6.0.100\"",
            "  },",
            "  \"tools\": {",
            "    \"dotnet\": \"6.0.100\"",
            "  }",
            "}",
        ];

        string[] expectedLines =
        [
            "{",
            "  \"sdk\": {",
            "    \"version\": \"10.0.100\"",
            "  },",
            "  \"tools\": {",
            "    \"dotnet\": \"10.0.100\"",
            "  }",
            "}",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(bom);
        string dockerfile = await fixture.Project.AddFileAsync("global.json", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("10.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<GlobalJsonUpgrader>();
        var target = new GlobalJsonUpgrader(fixture.Console, options, logger);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(dockerfile);

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
}
