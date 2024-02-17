// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper;

public class ProjectUpgraderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task UpgradeAsync_Does_Not_Throw()
    {
        // Arrange
        using var console = new TestConsole();
        var target = new ProjectUpgrader(console);

        // Act and Assert
        try
        {
            await Should.NotThrowAsync(() => target.UpgradeAsync(CancellationToken.None));
        }
        finally
        {
            outputHelper.WriteLine(console.Output);
        }
    }
}
