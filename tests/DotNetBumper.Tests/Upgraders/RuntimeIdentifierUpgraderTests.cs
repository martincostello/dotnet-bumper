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
            "11.0",
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
        var builder = Project
            .Create(hasSdk: true)
            .Property(propertyName, propertyValue);

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasByteOrderMark);
        string projectFile = await fixture.Project.AddFileAsync(fileName, builder.Xml, encoding);

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

        using (var stream = File.OpenRead(projectFile))
        {
            var bom = Encoding.UTF8.GetPreamble();
            var buffer = new byte[bom.Length];

            await stream.ReadExactlyAsync(buffer, 0, buffer.Length, fixture.CancellationToken);

            bom.Length.ShouldBeGreaterThan(0);
            bom.SequenceEqual(buffer).ShouldBe(hasByteOrderMark);
        }

        var xml = await fixture.Project.GetFileAsync(projectFile);
        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        var actualValue = project
            .Root
            .Element(ns + "PropertyGroup")?
            .Element(ns + propertyName)?
            .Value;

        actualValue.ShouldBe(expectedValue);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);

        fixture.LogContext.Changelog.ShouldContain("Update runtime identifiers");
    }

    [Fact]
    public async Task UpgradeAsync_Handles_Files_Without_Xml_Namespace()
    {
        // Arrange
        string fileContents =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0">
             <PropertyGroup>
               <Configuration>Release</Configuration>
               <Platform>Any CPU</Platform>
               <PublishDir>bin\Release\net6.0\publish\</PublishDir>
               <PublishProtocol>FileSystem</PublishProtocol>
               <TargetFramework>net6.0</TargetFramework>
               <RuntimeIdentifier>win10-x64</RuntimeIdentifier>
               <SelfContained>true</SelfContained>
               <PublishSingleFile>False</PublishSingleFile>
               <PublishReadyToRun>False</PublishReadyToRun>
               <PublishTrimmed>True</PublishTrimmed>
             </PropertyGroup>
            </Project>
            """;

        string expectedContent =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0">
             <PropertyGroup>
               <Configuration>Release</Configuration>
               <Platform>Any CPU</Platform>
               <PublishDir>bin\Release\net6.0\publish\</PublishDir>
               <PublishProtocol>FileSystem</PublishProtocol>
               <TargetFramework>net6.0</TargetFramework>
               <RuntimeIdentifier>win-x64</RuntimeIdentifier>
               <SelfContained>true</SelfContained>
               <PublishSingleFile>False</PublishSingleFile>
               <PublishReadyToRun>False</PublishReadyToRun>
               <PublishTrimmed>True</PublishTrimmed>
             </PropertyGroup>
            </Project>
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string pubxml = await fixture.Project.AddFileAsync("src/Project/Properties/PublishProfiles/Profile.pubxml", fileContents);

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

        string actualContent = await File.ReadAllTextAsync(pubxml, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Handles_Files_With_Xml_Namespace()
    {
        // Arrange
        string fileContents =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
             <PropertyGroup>
               <Configuration>Release</Configuration>
               <Platform>Any CPU</Platform>
               <PublishDir>bin\Release\net6.0\publish\</PublishDir>
               <PublishProtocol>FileSystem</PublishProtocol>
               <TargetFramework>net6.0</TargetFramework>
               <RuntimeIdentifier>win10-x64</RuntimeIdentifier>
               <SelfContained>true</SelfContained>
               <PublishSingleFile>False</PublishSingleFile>
               <PublishReadyToRun>False</PublishReadyToRun>
               <PublishTrimmed>True</PublishTrimmed>
             </PropertyGroup>
            </Project>
            """;

        string expectedContent =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
             <PropertyGroup>
               <Configuration>Release</Configuration>
               <Platform>Any CPU</Platform>
               <PublishDir>bin\Release\net6.0\publish\</PublishDir>
               <PublishProtocol>FileSystem</PublishProtocol>
               <TargetFramework>net6.0</TargetFramework>
               <RuntimeIdentifier>win-x64</RuntimeIdentifier>
               <SelfContained>true</SelfContained>
               <PublishSingleFile>False</PublishSingleFile>
               <PublishReadyToRun>False</PublishReadyToRun>
               <PublishTrimmed>True</PublishTrimmed>
             </PropertyGroup>
            </Project>
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string pubxml = await fixture.Project.AddFileAsync("src/Project/Properties/PublishProfiles/Profile.pubxml", fileContents);

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

        string actualContent = await File.ReadAllTextAsync(pubxml, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
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

        await fixture.Project.AddFileAsync("Project.csproj", content);

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
        actual.ShouldBe(expected);
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(properties, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(properties, fixture.CancellationToken);

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
