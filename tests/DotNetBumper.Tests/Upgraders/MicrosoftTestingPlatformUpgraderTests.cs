// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MartinCostello.DotNetBumper.Upgraders;

public class MicrosoftTestingPlatformUpgraderTests(ITestOutputHelper outputHelper)
{
    private const string Changelog = "Migrate test projects to use Microsoft Testing Platform";

    private static UpgradeInfo Upgrade => new()
    {
        Channel = Version.Parse("10.0"),
        EndOfLife = DateOnly.MaxValue,
        ReleaseType = DotNetReleaseType.Lts,
        SdkVersion = new("10.0.100"),
        SupportPhase = DotNetSupportPhase.Active,
    };

    [Fact]
    public async Task UpgradeAsync_Migrates_VSTest_Mode_Project_And_Updates_Global_Json()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string project =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
                <TestingPlatformCaptureOutput>false</TestingPlatformCaptureOutput>
                <TestingPlatformShowTestsFailure>true</TestingPlatformShowTestsFailure>
              </PropertyGroup>
            </Project>
            """;

        string projectPath = await fixture.Project.AddFileAsync("tests/Project.Tests/Project.Tests.csproj", project);
        string globalJsonPath = await fixture.Project.AddGlobalJsonAsync("10.0.100");

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.UpgradeAsync(Upgrade, fixture.CancellationToken);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
        fixture.LogContext.Changelog.ShouldContain(Changelog);

        string updatedProject = await File.ReadAllTextAsync(projectPath, fixture.CancellationToken);
        updatedProject.ShouldNotContain("TestingPlatformDotnetTestSupport");
        updatedProject.ShouldNotContain("TestingPlatformCaptureOutput");
        updatedProject.ShouldNotContain("TestingPlatformShowTestsFailure");

        await AssertRunnerEnabledAsync(globalJsonPath, fixture.CancellationToken);
    }

    [Fact]
    public async Task UpgradeAsync_Creates_Global_Json_When_None_Exists()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string project =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
                <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
              </PropertyGroup>
            </Project>
            """;

        await fixture.Project.AddFileAsync("tests/Project.Tests/Project.Tests.csproj", project);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.UpgradeAsync(Upgrade, fixture.CancellationToken);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
        fixture.LogContext.Changelog.ShouldContain(Changelog);

        string globalJsonPath = fixture.Project.GetFilePath("global.json");
        File.Exists(globalJsonPath).ShouldBeTrue();

        var runner = await AssertRunnerEnabledAsync(globalJsonPath, fixture.CancellationToken);
        runner.RootElement.GetProperty("sdk").GetProperty("version").GetString().ShouldBe("10.0.100");
    }

    [Fact]
    public async Task UpgradeAsync_Does_Nothing_When_Not_Using_VSTest_Mode()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string project =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """;

        await fixture.Project.AddFileAsync("tests/Project.Tests/Project.Tests.csproj", project);
        string globalJsonPath = await fixture.Project.AddGlobalJsonAsync("10.0.100");

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.UpgradeAsync(Upgrade, fixture.CancellationToken);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
        fixture.LogContext.Changelog.ShouldNotContain(Changelog);

        string globalJson = await File.ReadAllTextAsync(globalJsonPath, fixture.CancellationToken);
        globalJson.ShouldNotContain("runner");
    }

    private static async Task<JsonDocument> AssertRunnerEnabledAsync(string globalJsonPath, CancellationToken cancellationToken)
    {
        string globalJson = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);

        var document = JsonDocument.Parse(globalJson);

        document.RootElement
            .GetProperty("test")
            .GetProperty("runner")
            .GetString()
            .ShouldBe("Microsoft.Testing.Platform");

        return document;
    }

    private static MicrosoftTestingPlatformUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<MicrosoftTestingPlatformUpgrader>());
    }
}
