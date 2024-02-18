// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper;

public class EndToEndTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("6.0.100", new string[] { "net6.0", })]
    [InlineData("7.0.100", new string[] { "net6.0", "net7.0" })]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--channel=7.0")]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--channel=8.0")]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--channel=9.0")]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--upgrade-type=latest")]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--upgrade-type=lts")]
    [InlineData("6.0.100", new string[] { "net6.0", }, "--upgrade-type=preview")]
    public async Task Application_Upgrades_Project(
        string sdkVersion,
        string[] targetFrameworks,
        params string[] args)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var globalJson = CreateGlobalJson(sdkVersion);
        var project = CreateProjectXml(targetFrameworks);

        fixture.Project.AddDirectory("src");
        await fixture.Project.AddFileAsync("global.json", globalJson);
        await fixture.Project.AddFileAsync("src/Project.csproj", project);

        // Act
        int status = await RunAsync(fixture, args);

        // Assert
        status.ShouldBe(0);

        string? actualSdk = await GetSdkVersionAsync(fixture, "global.json");
        string? actualTfm = await GetTargetFrameworksAsync(fixture, "src/Project.csproj");

        actualSdk.ShouldNotBe(sdkVersion);
        actualTfm.ShouldNotBe(string.Join(';', targetFrameworks));
    }

    [Theory]
    [InlineData("1.0.100", "netcoreapp1.0", "--channel=1.0")]
    [InlineData("1.1.100", "netcoreapp1.1", "--channel=1.1")]
    [InlineData("2.0.100", "netcoreapp2.0", "--channel=2.0")]
    [InlineData("2.1.100", "netcoreapp2.1", "--channel=2.1")]
    [InlineData("2.2.100", "netcoreapp2.2", "--channel=2.2")]
    [InlineData("3.0.100", "netcoreapp3.0", "--channel=3.0")]
    [InlineData("3.1.100", "netcoreapp3.1", "--channel=3.1")]
    [InlineData("5.0.100", "net5.0", "--channel=5.0")]
    [InlineData("6.0.100", "net6.0", "--channel=5.0")]
    public async Task Application_Does_Not_Upgrade_Project(
        string sdkVersion,
        string targetFramework,
        params string[] args)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var globalJson = CreateGlobalJson(sdkVersion);
        var project = CreateProjectXml(targetFramework);

        fixture.Project.AddDirectory("src");
        await fixture.Project.AddFileAsync("global.json", globalJson);
        await fixture.Project.AddFileAsync("src/Project.csproj", project);

        // Act
        int status = await RunAsync(fixture, args);

        // Assert
        status.ShouldBe(0);

        string? actualSdk = await GetSdkVersionAsync(fixture, "global.json");
        string? actualTfm = await GetTargetFrameworksAsync(fixture, "src/Project.csproj");

        actualSdk.ShouldBe(sdkVersion);
        actualTfm.ShouldBe(targetFramework);
    }

    [Fact]
    public async Task Application_Validates_Project_Exists()
    {
        // Arrange
        string projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act
        int actual = await Program.Main([projectPath]);

        // Assert
        actual.ShouldBe(1);
    }

    [Fact]
    public async Task Application_Validates_Channel()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        // Act
        int actual = await RunAsync(fixture, "--channel=foo");

        // Assert
        actual.ShouldBe(1);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--version")]
    public async Task Application_Successfully_Invokes_Command(params string[] args)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        // Act
        int actual = await Program.Main([fixture.Project.DirectoryName, .. args]);

        // Assert
        actual.ShouldBe(0);
    }

    private static async Task<int> RunAsync(UpgraderFixture fixture, params string[] args)
    {
        static bool LogFilter(string? category, LogLevel level)
        {
            if (category is null)
            {
                return false;
            }

            return !(category.StartsWith("Microsoft", StringComparison.Ordinal) ||
                     category.StartsWith("Polly", StringComparison.Ordinal) ||
                     category.StartsWith("System", StringComparison.Ordinal));
        }

        return await Bumper.RunAsync(
            fixture.Console,
            [fixture.Project.DirectoryName, "--verbose", ..args],
            (builder) => builder.AddXUnit(fixture).AddFilter(LogFilter),
            CancellationToken.None);
    }

    private static string CreateGlobalJson(string sdkVersion)
    {
        return $$"""
                 {
                   "sdk": {
                     "version": "{{sdkVersion}}"
                   }
                 }
                 """;
    }

    private static XDocument CreateProjectXml(params string[] targetFrameworks)
    {
        string tfms = targetFrameworks.Length == 1
            ? $"<TargetFramework>{targetFrameworks[0]}</TargetFramework>"
            : $"<TargetFrameworks>{string.Join(";", targetFrameworks)}</TargetFrameworks>";

        string xml = $"""
                      <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                          {tfms}
                        </PropertyGroup>
                      </Project>
                      """;

        return XDocument.Parse(xml);
    }

    private static async Task<string?> GetSdkVersionAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var json = await fixture.Project.GetFileAsync(fileName);
        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("sdk")
            .GetProperty("version")
            .GetString();
    }

    private static async Task<string?> GetTargetFrameworksAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var xml = await fixture.Project.GetFileAsync(fileName);
        var project = XDocument.Parse(xml);

        var tfm = project
            .Root?
            .Element("PropertyGroup")?
            .Element("TargetFramework")?
            .Value;

        if (tfm is not null)
        {
            return tfm;
        }

        return project
            .Root?
            .Element("PropertyGroup")?
            .Element("TargetFrameworks")?
            .Value;
    }
}
