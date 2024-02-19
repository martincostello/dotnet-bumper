// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Upgrades;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgraders;

public class ServerlessUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("serverless.yml")]
    [InlineData("serverless.yaml")]
    public async Task UpgradeAsync_Upgrades_Serverless_Runtimes(string fileName)
    {
        // Arrange
        string serverless = string.Join(
            Environment.NewLine,
            [
                "# My Serverless Application",
                "service: my-application",
                string.Empty,
                "provider:",
                "  name: aws",
                "  architecture: arm64",
                "  memorySize: 256 # This is a comment",
                "  runtime: dotnet6",
                "  timeout: 5",
                string.Empty,
                "functions:",
                string.Empty,
                "  average-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                string.Empty,
                "  fast-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                "    runtime: dotnet6",
                "    timeout: 1",
                string.Empty,
                "  slow-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                "    runtime: dotnet6 # This is another comment",
                "    timeout: 10",
            ]) + Environment.NewLine;

        using var fixture = new UpgraderFixture(outputHelper);

        string serverlessFile = await fixture.Project.AddFileAsync(fileName, serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = new(2022, 11, 8),
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

        // Act
        bool actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBeTrue();

        string expectedContent = string.Join(
            Environment.NewLine,
            [
                "# My Serverless Application",
                "service: my-application",
                string.Empty,
                "provider:",
                "  name: aws",
                "  architecture: arm64",
                "  memorySize: 256 # This is a comment",
                "  runtime: dotnet8",
                "  timeout: 5",
                string.Empty,
                "functions:",
                string.Empty,
                "  average-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                string.Empty,
                "  fast-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                "    runtime: dotnet8",
                "    timeout: 1",
                string.Empty,
                "  slow-function:",
                "    handler: MyAssembly::MyNamespace.MyClass::MyMethod",
                "    runtime: dotnet8 # This is another comment",
                "    timeout: 10",
            ]) + Environment.NewLine;

        string actualContent = await File.ReadAllTextAsync(serverlessFile);
        actualContent.ShouldBe(expectedContent);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBeFalse();
    }
}
