// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class ContainerRegistryClientTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("rhysd/actionlint", "latest")]
    [InlineData("ghcr.io/martincostello/eurovision-hue", "main")]
    [InlineData("mcr.microsoft.com/dotnet/sdk", "latest")]
    [InlineData("public.ecr.aws/aquasecurity/trivy-db", "latest")]
    public async Task Can_Get_Latest_Image_Digest(string image, string tag)
    {
        // Arrange
        using var client = new HttpClient();
        var logger = outputHelper.ToLogger<ContainerRegistryClient>();

        var target = new ContainerRegistryClient(client, logger);

        // Act
        var actual = await target.GetImageDigestAsync(image, tag, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNullOrWhiteSpace();
        actual.ShouldStartWith("sha256:");

        // Arrange
        var original = actual;

        actual = await target.GetImageDigestAsync(image, tag, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(original);

        // Arrange - Get using the digest instead of the tag to validate it
        var digest = actual;

        // Act
        actual = await target.GetImageDigestAsync(image, digest, TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldBe(original);
    }
}
