// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;
using Spectre.Console;

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

        string globalJson =
            $$"""
            {
              "sdk": {
                "version": "{{sdkVersion}}"
              }
            }
            """;

        string tfmXml = targetFrameworks.Length == 1
            ? $"<TargetFramework>{targetFrameworks[0]}</TargetFramework>"
            : $"<TargetFrameworks>{string.Join(";", targetFrameworks)}</TargetFrameworks>";

        string projectXml =
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {tfmXml}
              </PropertyGroup>
            </Project>
            """;

        fixture.Project.AddDirectory("src");
        await fixture.Project.AddFileAsync("global.json", globalJson);
        await fixture.Project.AddFileAsync("src/Project.csproj", projectXml);

        // Act
        int status = await Program.Main([fixture.Project.DirectoryName, "--verbose", ..args]);

        // Assert
        status.ShouldBe(0);

        string? actualSdk = await GetSdkVersionAsync(fixture, "global.json");
        string? actualTfm = await GetTargetFrameworksAsync(fixture, "src/Project.csproj");

        fixture.Console.WriteLine($".NET SDK version: {actualSdk}");
        fixture.Console.WriteLine($"Target Framework(s): {actualTfm}");

        actualSdk.ShouldNotBe(sdkVersion);
        actualTfm.ShouldNotBe(string.Join(';', targetFrameworks));
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
