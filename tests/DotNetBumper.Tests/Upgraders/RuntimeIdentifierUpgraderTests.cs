// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace MartinCostello.DotNetBumper.Upgraders;

public class RuntimeIdentifierUpgraderTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<string, string, bool, string, string, string> TargetFrameworks()
    {
        string[] channels =
        [
            "8.0",
            "9.0",
            "10.0",
        ];

        string[] fileNames =
        [
            "Directory.Build.props",
            "src/MyProject.csproj",
            "src/MyProject.fsproj",
            "src/MyProject/Properties/PublishProfiles/profile.pubxml",
        ];

        var testCases = new TheoryData<string, string, bool, string, string, string>();

        foreach (var channel in channels)
        {
            foreach (var fileName in fileNames)
            {
                foreach (var hasByteOrderMark in new[] { false, true })
                {
                    testCases.Add(channel, fileName, hasByteOrderMark, "DefaultRuntimeIdentifier", "win10-x64", "win-x64");
                    testCases.Add(channel, fileName, hasByteOrderMark, "PublishDir", "bin/Release/publish/win10-x64", "bin/Release/publish/win-x64");
                    testCases.Add(channel, fileName, hasByteOrderMark, "PublishDir", "bin\\Release\\publish\\win10-x64", "bin\\Release\\publish\\win-x64");
                    testCases.Add(channel, fileName, hasByteOrderMark, "RuntimeIdentifier", "win10-x64", "win-x64");
                    testCases.Add(channel, fileName, hasByteOrderMark, "RuntimeIdentifiers", "linux-x64;osx-x64;win10-x64", "linux-x64;osx-x64;win-x64");
                }
            }
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(TargetFrameworks))]
    public async Task UpgradeAsync_Upgrades_Properties(
        string channel,
        string fileName,
        bool hasByteOrderMark,
        string propertyName,
        string propertyValue,
        string expectedValue)
    {
        // Arrange
        string fileContents =
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <{propertyName}>{propertyValue}</{propertyName}>
               </PropertyGroup>
             </Project>
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasByteOrderMark);
        string projectFile = await fixture.Project.AddFileAsync(fileName, fileContents, encoding);

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

        using (var stream = File.OpenRead(projectFile))
        {
            var bom = Encoding.UTF8.GetPreamble();
            var buffer = new byte[bom.Length];

            await stream.ReadExactlyAsync(buffer, 0, buffer.Length);

            bom.Length.ShouldBeGreaterThan(0);
            bom.SequenceEqual(buffer).ShouldBe(hasByteOrderMark);
        }

        string actualContent = await File.ReadAllTextAsync(projectFile, encoding);

        var xml = await fixture.Project.GetFileAsync(projectFile);
        var project = XDocument.Parse(xml);

        var actualValue = project
            .Root?
            .Element("PropertyGroup")?
            .Element(propertyName)?
            .Value;

        actualValue.ShouldBe(expectedValue);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);

        fixture.LogContext.Changelog.ShouldContain("Update runtime identifiers");
    }

    [Theory]
    [InlineData("Not XML", ProcessingResult.Warning)]
    [InlineData("<>", ProcessingResult.Warning)]
    [InlineData("<Project></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup></PropertyGroup></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty></SomeProperty></PropertyGroup></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty>;;</SomeProperty></PropertyGroup></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty>8.0</SomeProperty></PropertyGroup></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty>SomeValue</SomeProperty></PropertyGroup></Project>", ProcessingResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty>net462</SomeProperty></PropertyGroup></Project>", ProcessingResult.None)]
    public async Task UpgradeAsync_Handles_Invalid_Xml(string content, ProcessingResult expected)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string project = await fixture.Project.AddFileAsync("Project.csproj", content);

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

    [Fact]
    public async Task UpgradeAsync_Does_Not_Change_DotNet_7_Runtime_Identifiers()
    {
        // Arrange
        string fileContents =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <RuntimeIdentifier>win10-x64</RuntimeIdentifier>
              </PropertyGroup>
            </Project>
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        var projectFile = await fixture.Project.AddFileAsync("Directory.Build.props", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("7.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("7.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);

        string actualContent = await File.ReadAllTextAsync(projectFile);
        actualContent.ShouldBe(fileContents);
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Bom(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <!-- This is a comment -->",
            "    <RuntimeIdentifier>win10-x64</RuntimeIdentifier>",
            "  </PropertyGroup>",
            "</Project>",
        ];

        string[] expectedLines =
        [
            "<Project Sdk=\"Microsoft.NET.Sdk\">",
            "  <PropertyGroup>",
            "    <!-- This is a comment -->",
            "    <RuntimeIdentifier>win-x64</RuntimeIdentifier>",
            "  </PropertyGroup>",
            "</Project>",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string properties = await fixture.Project.AddFileAsync("Directory.Build.props", fileContents, encoding);

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

        string actualContent = await File.ReadAllTextAsync(properties);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(properties);

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

    private static RuntimeIdentifierUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<RuntimeIdentifierUpgrader>());
    }
}
