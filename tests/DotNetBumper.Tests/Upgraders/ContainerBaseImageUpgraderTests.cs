// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace MartinCostello.DotNetBumper.Upgraders;

public class ContainerBaseImageUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Dockerfile(string channel)
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

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
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

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
