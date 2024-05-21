// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Build.Utilities.ProjectCreation;
using NSubstitute;

namespace MartinCostello.DotNetBumper.PostProcessors;

public class DotNetTestPostProcessorTests(ITestOutputHelper outputHelper)
{
    private static TimeSpan Timeout { get; } = TimeSpan.FromMinutes(4);

    public static TheoryData<string> Channels()
    {
#pragma warning disable IDE0028 // See https://github.com/dotnet/roslyn/issues/72668
        return new()
        {
            "8.0",
            "9.0",
        };
#pragma warning restore IDE0028
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task PostProcessAsync_Succeeds_When_No_DirectoryBuildProps(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade);

        var target = CreateTarget(fixture);

        using var cts = new CancellationTokenSource(Timeout);

        // Act
        var actual = await target.PostProcessAsync(upgrade, cts.Token);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task PostProcessAsync_Succeeds_When_DirectoryBuildProps_Without_ArtifactsOutput(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade);

        await fixture.Project.AddDirectoryBuildPropsAsync();

        var target = CreateTarget(fixture);

        using var cts = new CancellationTokenSource(Timeout);

        // Act
        var actual = await target.PostProcessAsync(upgrade, cts.Token);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
    }

    [Theory]
    [InlineData("8.0", "", "")]
    [InlineData("8.0", "false", "")]
    [InlineData("8.0", "true", "")]
    [InlineData("8.0", "true", "$(MSBuildThisFileDirectory)\\.artifacts")]
    [InlineData("9.0", "", "")]
    [InlineData("9.0", "false", "")]
    [InlineData("9.0", "true", "")]
    [InlineData("9.0", "true", "$(MSBuildThisFileDirectory)\\.artifacts")]
    public async Task PostProcessAsync_Succeeds_When_DirectoryBuildProps_With_Artifacts_Output(
        string channel,
        string useArtifactsOutput,
        string artifactsPath)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade);

        fixture.UserConfiguration.NoWarn = ["CA1002", "CA1515"];

        var properties = ProjectCreator.Create()
            .Property("AnalysisMode", "All")
            .Property("ArtifactsPath", artifactsPath)
            .Property("EnableNETAnalyzers", true)
            .Property("NoWarn", "$(NoWarn);CA1307;CA1309;CA1707;CA1819")
            .Property("TreatWarningsAsErrors", true)
            .Property("UseArtifactsOutput", useArtifactsOutput);

        await fixture.Project.AddFileAsync("Directory.Build.props", properties.Xml);

        var target = CreateTarget(fixture);

        using var cts = new CancellationTokenSource(Timeout);

        // Act
        var actual = await target.PostProcessAsync(upgrade, cts.Token);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
    }

    private static DotNetTestPostProcessor CreateTarget(UpgraderFixture fixture)
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
            environment,
            configurationProvider,
            fixture.LogContext,
            options,
            fixture.CreateLogger<DotNetTestPostProcessor>());
    }

    private async Task<UpgradeInfo> GetUpgradeAsync(string channel)
    {
        // Use the same SDK version as the upgrade to prevent a different dotnet format version being used
        var finder = new DotNetUpgradeFinder(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new UpgradeOptions() { DotNetChannel = channel }),
            outputHelper.ToLogger<DotNetUpgradeFinder>());

        using var cts = new CancellationTokenSource(Timeout);

        var upgrade = await finder.GetUpgradeAsync(cts.Token);
        upgrade.ShouldNotBeNull();

        return upgrade;
    }

    private async Task<UpgraderFixture> CreateFixtureAsync(UpgradeInfo upgrade)
    {
        var fixture = new UpgraderFixture(outputHelper);

        try
        {
            string[] targetFrameworks = [$"net{upgrade.Channel}"];

            await fixture.Project.AddGitIgnoreAsync();

            await fixture.Project.AddApplicationProjectAsync(targetFrameworks);
            await fixture.Project.AddGlobalJsonAsync(upgrade.SdkVersion.ToString());

            await fixture.Project.AddTestProjectAsync(targetFrameworks);
            await fixture.Project.AddUnitTestsAsync();

            return fixture;
        }
        catch (Exception)
        {
            fixture.Dispose();
            throw;
        }
    }
}
