// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

public class DotNetProcessTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task TryGetSdkVersionAsync_Returns_Valid_Version_String()
    {
        // Arrange
        var logger = outputHelper.ToLogger<DotNetProcess>();
        var target = new DotNetProcess(logger);

        // Act
        var actual = await target.TryGetSdkVersionAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNullOrWhiteSpace();
        NuGetVersion.TryParse(actual, out _).ShouldBeTrue();
    }
}
