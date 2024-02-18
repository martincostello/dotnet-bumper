// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Upgrades;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MartinCostello.DotNetBumper;

public class ProjectUpgraderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task UpgradeAsync_Does_Not_Throw()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        var target = CreateTarget(fixture);

        // Act and Assert
        await Should.NotThrowAsync(() => target.UpgradeAsync(CancellationToken.None));
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

        var dotnet = new DotNetProcess(outputHelper.ToLogger<DotNetProcess>());
        var finder = new DotNetUpgradeFinder(
            new HttpClient(),
            options,
            outputHelper.ToLogger<DotNetUpgradeFinder>());

        return new ProjectUpgrader(
            fixture.Console,
            dotnet,
            finder,
            [Substitute.For<IUpgrader>()],
            TimeProvider.System,
            options,
            outputHelper.ToLogger<ProjectUpgrader>());
    }
}
