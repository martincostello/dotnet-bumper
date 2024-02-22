// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
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
            ]) + Environment.NewLine;

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

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

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
            ]) + Environment.NewLine;

        string actualContent = await File.ReadAllTextAsync(serverlessFile);
        actualContent.ShouldBe(expectedContent);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("dotnetcore1.0")]
    [InlineData("dotnetcore2.0")]
    [InlineData("dotnetcore2.1")]
    [InlineData("dotnetcore3.1")]
    [InlineData("dotnet5.0")]
    [InlineData("go1.x")]
    [InlineData("java8")]
    [InlineData("java8.al2")]
    [InlineData("java11")]
    [InlineData("java17")]
    [InlineData("java21")]
    [InlineData("nodejs")]
    [InlineData("nodejs4.3")]
    [InlineData("nodejs4.3-edge")]
    [InlineData("nodejs6.10")]
    [InlineData("nodejs8.10")]
    [InlineData("nodejs10.x")]
    [InlineData("nodejs12.x")]
    [InlineData("nodejs14.x")]
    [InlineData("nodejs16.x")]
    [InlineData("nodejs18.x")]
    [InlineData("nodejs20.x")]
    [InlineData("provided")]
    [InlineData("provided.al2")]
    [InlineData("provided.al2023")]
    [InlineData("python2.7")]
    [InlineData("python3.6")]
    [InlineData("python3.7")]
    [InlineData("python3.8")]
    [InlineData("python3.9")]
    [InlineData("python3.10")]
    [InlineData("python3.11")]
    [InlineData("python3.12")]
    [InlineData("ruby2.5")]
    [InlineData("ruby2.7")]
    [InlineData("ruby3.2")]
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

        string serverlessFile = await fixture.Project.AddFileAsync("serverless.yml", serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
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

        string serverlessFile = await fixture.Project.AddFileAsync("serverless.yml", serverless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(version, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = releaseType,
            SdkVersion = new($"{version}.0.100"),
            SupportPhase = supportPhase,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

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

        string serverlessFile = await fixture.Project.AddFileAsync("serverless.yml", invalidServerless);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.Warning);
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r", false)]
    [InlineData("\r", true)]
    [InlineData("\r\n", false)]
    [InlineData("\r\n", true)]
    public async Task UpgradeAsync_Preserves_Line_Endings(string newLine, bool bom)
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

        var encoding = new UTF8Encoding(bom);
        string dockerfile = await fixture.Project.AddFileAsync("serverless.yaml", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("10.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var options = Options.Create(new UpgradeOptions() { ProjectPath = fixture.Project.DirectoryName });
        var logger = outputHelper.ToLogger<ServerlessUpgrader>();
        var target = new ServerlessUpgrader(fixture.Console, options, logger);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(dockerfile);

        if (bom)
        {
            actualBytes.ShouldStartWithUTF8Bom();
        }
        else
        {
            actualBytes.ShouldNotStartWithUTF8Bom();
        }

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }
}
