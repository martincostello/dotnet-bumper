// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

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
        UpgradeOptions? options = null)
    {
        options ??= new UpgradeOptions()
        {
            ProjectPath = fixture.Project.Path,
        };

        var logger = outputHelper.ToLogger<ProjectUpgrader>();

        return new ProjectUpgrader(fixture.Console, Options.Create(options), logger);
    }
}
