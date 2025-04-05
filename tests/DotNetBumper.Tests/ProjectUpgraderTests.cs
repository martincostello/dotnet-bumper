// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.PostProcessors;
using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

public class ProjectUpgraderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task UpgradeAsync_Does_Not_Throw()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        var target = CreateTarget(fixture);

        // Act
        int actual = await target.UpgradeAsync(fixture.CancellationToken);

        // Assert
        actual.ShouldBe(0);
    }

    private ProjectUpgrader CreateTarget(
        UpgraderFixture fixture,
        UpgradeOptions? upgradeOptions = null)
    {
        upgradeOptions ??= new UpgradeOptions()
        {
            ProjectPath = fixture.Project.DirectoryName,
        };

        var options = Options.Create(upgradeOptions);

        var environment = Substitute.For<IEnvironment>();

        // Return the opposite of the real environment for coverage
        environment.IsGitHubActions
                   .Returns(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true");

        environment.SupportsLinks
                   .Returns(!AnsiConsole.Profile.Capabilities.Links);

        var finder = new DotNetUpgradeFinder(
            new HttpClient(),
            TimeProvider.System,
            options,
            outputHelper.ToLogger<DotNetUpgradeFinder>());

        var upgrader = Substitute.For<IUpgrader>();

        upgrader.UpgradeAsync(Arg.Any<UpgradeInfo>(), Arg.Any<CancellationToken>())
                .Returns(ProcessingResult.Success);

        var postProcessor = Substitute.For<IPostProcessor>();

        postProcessor.PostProcessAsync(Arg.Any<UpgradeInfo>(), Arg.Any<CancellationToken>())
                     .Returns(ProcessingResult.Success);

        return new ProjectUpgrader(
            fixture.Console,
            environment,
            finder,
            [upgrader],
            [postProcessor],
            fixture.LogContext,
            TimeProvider.System,
            options,
            outputHelper.ToLogger<ProjectUpgrader>());
    }
}
