// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NSubstitute;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper.Upgraders;

public class PackageVersionUpgraderTests(ITestOutputHelper outputHelper)
{
    private static TimeSpan Timeout { get; } = TimeSpan.FromMinutes(4);

    [Fact]
    public async Task UpgradeAsync_Does_Not_Include_Prerelease_Packages()
    {
        // Arrange
        string[] targetFrameworks = ["net6.0"];

        var upgrade = await GetUpgradeAsync("8.0");

        using var fixture = new UpgraderFixture(outputHelper)
        {
            UpgradeType = UpgradeType.Lts,
        };

        fixture.Project.AddGitRepository();
        await fixture.Project.AddGitIgnoreAsync();
        await fixture.Project.AddEditorConfigAsync();

        await fixture.Project.AddSolutionAsync("Project.sln");

        await fixture.Project.AddDirectoryBuildPropsAsync();
        await fixture.Project.AddToolManifestAsync();

        await fixture.Project.AddGlobalJsonAsync(upgrade.SdkVersion.ToString());

        await fixture.Project.AddApplicationProjectAsync(targetFrameworks);

        string dependencyName = "Microsoft.Extensions.Configuration";
        string dependencyVersion = "6.0.2-mauipre.1.22102.15";

        string testProject = await fixture.Project.AddTestProjectAsync(
            targetFrameworks,
            [KeyValuePair.Create(dependencyName, dependencyVersion)]);

        await fixture.Project.AddUnitTestsAsync();

        var target = CreateTarget(fixture);

        using var cts = new CancellationTokenSource(Timeout);

        // Act
        var actual = await target.UpgradeAsync(upgrade, cts.Token);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);

        var upgradedReferences = await ProjectAssertionHelpers.GetPackageReferencesAsync(fixture, testProject);

        upgradedReferences.ShouldContainKey(dependencyName);
        upgradedReferences.ShouldNotContainValueForKey(dependencyName, dependencyVersion);

        NuGetVersion.TryParse(upgradedReferences[dependencyName], out var version).ShouldBeTrue();
        version.IsPrerelease.ShouldBeFalse();
    }

    private static PackageVersionUpgrader CreateTarget(UpgraderFixture fixture)
    {
        var environment = Substitute.For<IEnvironment>();
        environment.SupportsLinks.Returns(true);

        var options = fixture.CreateOptions();
        options.Value.TestUpgrade = true;

        var configurationLoader = Substitute.For<BumperConfigurationLoader>(
            options,
            fixture.CreateLogger<BumperConfigurationLoader>());

        configurationLoader.LoadAsync(Arg.Any<CancellationToken>())
                           .Returns(fixture.UserConfiguration);

        var configurationProvider = new BumperConfigurationProvider(
            configurationLoader,
            options,
            fixture.CreateLogger<BumperConfigurationProvider>());

        return new(
            new(fixture.CreateLogger<DotNetProcess>()),
            fixture.Console,
            fixture.Environment,
            configurationProvider,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<PackageVersionUpgrader>());
    }

    private async Task<UpgradeInfo> GetUpgradeAsync(string channel)
    {
        var finder = new DotNetUpgradeFinder(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new UpgradeOptions() { DotNetChannel = channel }),
            outputHelper.ToLogger<DotNetUpgradeFinder>());

        using var cts = new CancellationTokenSource(Timeout);

        var upgrade = await finder.GetUpgradeAsync(cts.Token);
        upgrade.ShouldNotBeNull();

        return upgrade;
    }
}
