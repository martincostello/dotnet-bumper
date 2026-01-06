// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;

namespace MartinCostello.DotNetBumper.Upgraders;

public class ContainerRegistryClientTests(ITestOutputHelper outputHelper)
{
    [Trait("Category", "End-to-End")]
    [Theory]
    [InlineData("rhysd/actionlint", "latest")]
    [InlineData("ghcr.io/martincostello/eurovision-hue", "main")]
    [InlineData("mcr.microsoft.com/dotnet/sdk", "latest")]
    [InlineData("public.ecr.aws/aquasecurity/trivy-db", "latest")]
    public async Task Can_Get_Latest_Image_Digest(string image, string tag)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = new HttpClient();

        // Arrange
        var logger = outputHelper.ToLogger<ContainerRegistryClient>();
        var target = new ContainerRegistryClient(client, ContainerDigestCache.Instance, logger);

        // Act
        var actual = await target.GetImageDigestAsync(image, tag, cancellationToken);

        // Assert
        actual.ShouldNotBeNullOrWhiteSpace();
        actual.ShouldStartWith("sha256:");

        // Arrange
        var original = actual;

        actual = await target.GetImageDigestAsync(image, tag, cancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBe(original);

        // Arrange - Get using the digest instead of the tag to validate it
        var digest = actual;

        // Act
        actual = await target.GetImageDigestAsync(image, digest, cancellationToken);

        // Assert
        actual.ShouldBe(original);
    }

    [Theory]
    [InlineData("rhysd/actionlint", "latest", "sha256:887a259a5a534f3c4f36cb02dca341673c6089431057242cdc931e9f133147e9")]
    [InlineData("ghcr.io/martincostello/eurovision-hue", "main", "sha256:b97f5e6a072557a07a03789bafae1758f7b976b016de7e8d0bb941b560ccef52")]
    [InlineData("mcr.microsoft.com/dotnet/sdk", "latest", "sha256:b768b444028d3c531de90a356836047e48658cd1e26ba07a539a6f1a052a35d9")]
    [InlineData("public.ecr.aws/aquasecurity/trivy-db", "latest", "sha256:fd5ce44419bff589cccea0956c7d76b718e6b902bf9f664fe969bccb4038db1d")]
    public async Task Can_Get_Latest_Image_Digest_Using_Snapshot(string image, string tag, string expectedDigest)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        var options = new HttpClientInterceptorOptions().ThrowsOnMissingRegistration();
        await options.RegisterBundleFromResourceStreamAsync("container-registries", cancellationToken: cancellationToken);

        using var client = options.CreateHttpClient();

        // Arrange
        var digestCache = new ContainerDigestCache();
        var logger = outputHelper.ToLogger<ContainerRegistryClient>();
        var target = new ContainerRegistryClient(client, digestCache, logger);

        // Act
        var actual = await target.GetImageDigestAsync(image, tag, cancellationToken);

        // Assert
        actual.ShouldBe(expectedDigest);
    }
}
