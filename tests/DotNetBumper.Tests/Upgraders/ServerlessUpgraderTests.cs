// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class ServerlessUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("serverless.yml")]
    [InlineData("serverless.yaml")]
    public async Task UpgradeAsync_Upgrades_Serverless_Runtimes(string fileName)
    {
        // Arrange
        string[] lines =
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
            "    handler: MyAssembly::MyNamespace.MyClass::AverageFunction",
            string.Empty,
            "  fast-function:",
            "    handler: MyAssembly::MyNamespace.MyClass::FastFunction",
            "    runtime: dotnet6",
            "    timeout: 1",
            string.Empty,
            "  slow-function:",
            "    handler: MyAssembly::MyNamespace.MyClass::SlowFunction",
            "    runtime: dotnet6 # This is another comment",
            "    timeout: 10",
        ];

        string serverless = string.Join(Environment.NewLine, lines) + Environment.NewLine;

        using var fixture = new UpgraderFixture(outputHelper);

        string serverlessFile = await fixture.Project.AddFileAsync(fileName, serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        lines =
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
            "    handler: MyAssembly::MyNamespace.MyClass::AverageFunction",
            string.Empty,
            "  fast-function:",
            "    handler: MyAssembly::MyNamespace.MyClass::FastFunction",
            "    runtime: dotnet8",
            "    timeout: 1",
            string.Empty,
            "  slow-function:",
            "    handler: MyAssembly::MyNamespace.MyClass::SlowFunction",
            "    runtime: dotnet8 # This is another comment",
            "    timeout: 10",
        ];

        string expectedContent = string.Join(Environment.NewLine, lines) + Environment.NewLine;

        string actualContent = await File.ReadAllTextAsync(serverlessFile);
        actualContent.ShouldBe(expectedContent);
        fixture.LogContext.Changelog.ShouldNotBeEmpty();

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [ClassData(typeof(LambdaRuntimeTestData))]
    public async Task UpgradeAsync_Does_Not_Upgrade_From_Unsupported_Runtimes(string runtime)
    {
        // Arrange
        string serverless =
            $"""
             provider:
               runtime: {runtime}
             functions:
               average-function:
                 handler: MyAssembly::MyNamespace.MyClass::MyMethod
                 runtime: {runtime}
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("serverless.yml", serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(5, DotNetReleaseType.Sts, DotNetSupportPhase.Eol, ProcessingResult.None)]
    [InlineData(7, DotNetReleaseType.Sts, DotNetSupportPhase.Active, ProcessingResult.Warning)]
    [InlineData(9, DotNetReleaseType.Sts, DotNetSupportPhase.Preview, ProcessingResult.Warning)]
    [InlineData(9, DotNetReleaseType.Sts, DotNetSupportPhase.Active, ProcessingResult.Warning)]
    [InlineData(10, DotNetReleaseType.Lts, DotNetSupportPhase.Preview, ProcessingResult.Warning)]
    [InlineData(10, DotNetReleaseType.Lts, DotNetSupportPhase.GoLive, ProcessingResult.Warning)]
    public async Task UpgradeAsync_Does_Not_Upgrade_To_Unsupported_Runtimes(
        int version,
        DotNetReleaseType releaseType,
        DotNetSupportPhase supportPhase,
        ProcessingResult expected)
    {
        // Arrange
        string serverless =
            """
            provider:
              runtime: dotnet6
            functions:
              average-function:
                handler: MyAssembly::MyNamespace.MyClass::MyMethod
                runtime: dotnet6
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("serverless.yml", serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(version, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = releaseType,
            SdkVersion = new($"{version}.0.100"),
            SupportPhase = supportPhase,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task UpgradeAsync_Handles_Invalid_Yaml()
    {
        // Arrange
        string invalidServerless =
            """
            foo: bar
            baz
            """;

        using var fixture = new UpgraderFixture(outputHelper);
        await fixture.Project.AddFileAsync("serverless.yml", invalidServerless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.Warning);
        fixture.LogContext.Changelog.ShouldBeEmpty();
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Line_Endings(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "service: my-application",
            "provider:",
            "  name: aws",
            "  runtime: dotnet6",
        ];

        string[] expectedLines =
        [
            "service: my-application",
            "provider:",
            "  name: aws",
            "  runtime: dotnet10",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string serverlessFile = await fixture.Project.AddFileAsync("serverless.yaml", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("10.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(serverlessFile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(serverlessFile);

        if (hasUtf8Bom)
        {
            actualBytes.ShouldStartWithUTF8Bom();
        }
        else
        {
            actualBytes.ShouldNotStartWithUTF8Bom();
        }

        fixture.LogContext.Changelog.ShouldContain($"Update AWS Lambda runtime to `dotnet{upgrade.Channel.Major}`");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    private static ServerlessUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<ServerlessUpgrader>());
    }
}
