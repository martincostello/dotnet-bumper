// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace MartinCostello.DotNetBumper.Upgraders;

public class ContainerBaseImageUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_ContainerBaseImage(string channel)
    {
        // Arrange
        var builder = Project
            .Create(hasSdk: true)
            .Property("ContainerBaseImage", "mcr.microsoft.com/dotnet/nightly/runtime-deps:6.0-noble-chiseled-extra");

        using var fixture = new UpgraderFixture(outputHelper);

        string projectFile = await fixture.Project.AddFileAsync("MyProject.csproj", builder.Xml);

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

        var xml = await fixture.Project.GetFileAsync(projectFile);
        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        var actualValue = project
            .Root
            .Element(ns + "PropertyGroup")?
            .Element(ns + "ContainerBaseImage")?
            .Value;

        actualValue.ShouldBe($"mcr.microsoft.com/dotnet/nightly/runtime-deps:{channel}-noble-chiseled-extra");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("8.0", "bionic", "jammy")]
    [InlineData("8.0", "focal", "jammy")]
    [InlineData("9.0", "jammy", "noble")]
    [InlineData("9.0", "bionic", "noble")]
    [InlineData("9.0", "focal", "noble")]
    [InlineData("10.0", "bionic", "noble")]
    [InlineData("10.0", "focal", "noble")]
    [InlineData("10.0", "jammy", "noble")]
    [InlineData("11.0", "bionic", "resolute")]
    [InlineData("11.0", "focal", "resolute")]
    [InlineData("11.0", "jammy", "resolute")]
    [InlineData("11.0", "noble", "resolute")]
    [InlineData("8.0", "focal-chiseled-extra", "jammy-chiseled-extra")]
    [InlineData("9.0", "focal-chiseled-extra", "noble-chiseled-extra")]
    [InlineData("9.0", "jammy-chiseled-extra", "noble-chiseled-extra")]
    [InlineData("10.0", "jammy-chiseled-extra", "noble-chiseled-extra")]
    [InlineData("11.0", "noble-chiseled-extra", "resolute-chiseled-extra")]
    public async Task UpgradeAsync_Upgrades_ContainerFamily(string channel, string value, string expected)
    {
        // Arrange
        var builder = Project
            .Create(hasSdk: true)
            .Property("ContainerFamily", value);

        using var fixture = new UpgraderFixture(outputHelper);

        string projectFile = await fixture.Project.AddFileAsync("MyProject.csproj", builder.Xml);

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

        var xml = await fixture.Project.GetFileAsync(projectFile);
        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        var actualValue = project
            .Root
            .Element(ns + "PropertyGroup")?
            .Element(ns + "ContainerFamily")?
            .Value;

        actualValue.ShouldBe(expected);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    private static ContainerBaseImageUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<ContainerBaseImageUpgrader>());
    }
}
