// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgraders;

public class TargetFrameworkUpgraderTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<string, string, string, string, string> TargetFrameworks()
    {
        string[] channels = ["7.0", "8.0", "9.0"];
        string[] fileNames = ["Directory.Build.props", "src/MyProject.csproj", "src/MyProject.fsproj"];

        var testCases = new TheoryData<string, string, string, string, string>();

        foreach (var channel in channels)
        {
            foreach (var fileName in fileNames)
            {
                testCases.Add(channel, fileName, "DefaultTargetFramework", "net6.0", $"net{channel}");
                testCases.Add(channel, fileName, "TargetFramework", "net6.0", $"net{channel}");
                testCases.Add(channel, fileName, "TargetFrameworks", "net5.0;net6.0", $"net5.0;net6.0;net{channel}");
                testCases.Add(channel, fileName, "TargetFrameworks", "net5.0;;;;net6.0", $"net5.0;;;;net6.0;net{channel}");
            }
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(TargetFrameworks))]
    public async Task UpgradeAsync_Upgrades_Properties(
        string channel,
        string fileName,
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

        string projectFile = await fixture.Project.AddFileAsync(fileName, fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<TargetFrameworkUpgrader>();
        var target = new TargetFrameworkUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(UpgradeResult.Success);

        string actualContent = await File.ReadAllTextAsync(projectFile);

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
        actualUpdated.ShouldBe(UpgradeResult.None);
    }

    [Theory]
    [InlineData("Not XML", UpgradeResult.Warning)]
    [InlineData("<>", UpgradeResult.Warning)]
    [InlineData("<Project></Project>", UpgradeResult.None)]
    [InlineData("<Project><PropertyGroup></PropertyGroup></Project>", UpgradeResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty></SomeProperty></PropertyGroup></Project>", UpgradeResult.None)]
    [InlineData("<Project><PropertyGroup><SomeProperty>;;</SomeProperty></PropertyGroup></Project>", UpgradeResult.None)]
    public async Task UpgradeAsync_Handles_Invalid_Xml(string content, UpgradeResult expected)
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
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<TargetFrameworkUpgrader>();
        var target = new TargetFrameworkUpgrader(fixture.Console, options, logger);

        // Act
        UpgradeResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(expected);
    }
}
