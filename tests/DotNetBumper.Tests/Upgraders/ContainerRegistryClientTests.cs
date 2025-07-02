// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class ContainerRegistryClientTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("rhysd/actionlint", "latest")]
    [InlineData("mcr.microsoft.com/dotnet/sdk", "latest")]
    public async Task Can_Get_Latest_Image_Digest(string image, string tag)
    {
        // Arrange
        using var client = new HttpClient();
        var logger = outputHelper.ToLogger<ContainerRegistryClient>();

        var target = new ContainerRegistryClient(client, logger);

        // Act
        var actual = await target.GetImageDigestAsync(image, tag, CancellationToken.None);

        // Assert
        actual.ShouldNotBeNullOrWhiteSpace();
    }
}
