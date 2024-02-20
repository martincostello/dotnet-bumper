// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper;

public class EndToEndTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<BumperTestCase> TestCases()
    {
        var testCases = new TheoryData<BumperTestCase>
        {
            new("6.0.100", ["net6.0"]),
            new("7.0.100", ["net6.0", "net7.0"]),
            new("6.0.100", ["net6.0"], ["--channel=7.0"]),
            new("6.0.100", ["net6.0"], ["--channel=8.0"]),
            new("6.0.100", ["net6.0"], ["--channel=9.0"]),
            new("6.0.100", ["net6.0"], ["--upgrade-type=latest"]),
            new("6.0.100", ["net6.0"], ["--upgrade-type=lts"]),
            new("6.0.100", ["net6.0"], ["--upgrade-type=preview"]),
            new("6.0.100", ["net6.0"], [], Packages(("System.Text.Json", "6.0.0"))),
            new("7.0.100", ["net7.0"], [], Packages(("System.Text.Json", "7.0.0"))),
            new("8.0.100", ["net8.0"], ["--upgrade-type=preview"], Packages(("System.Text.Json", "8.0.0"))),
        };

        return testCases;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task Application_Upgrades_Project(BumperTestCase testCase)
    {
        // Arrange
        var testPackages = new Dictionary<string, string>()
        {
            ["Microsoft.NET.Test.Sdk"] = "17.9.0",
            ["xunit"] = "2.7.0",
            ["xunit.runner.visualstudio"] = "2.5.7",
        };

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddSolutionAsync("Project.sln");
        await fixture.Project.AddToolManifestAsync();

        string globalJson = await fixture.Project.AddGlobalJsonAsync(testCase.SdkVersion);
        string vscode = await fixture.Project.AddVisualStudioCodeLaunchConfigurationsAsync();
        string vsconfig = await fixture.Project.AddVisualStudioConfigurationAsync();

        string appProject = await fixture.Project.AddProjectAsync(
            "src/Project/Project.csproj",
            testCase.TargetFrameworks,
            testCase.PackageReferences);

        string testProject = await fixture.Project.AddProjectAsync(
            "tests/Project.Tests/Project.Tests.csproj",
            testCase.TargetFrameworks,
            testPackages);

        string unitTestClass =
            """
            using Xunit;

            namespace MyProject.Tests;

            public static class UnitTests
            {
                [Fact]
                public static void Always_Passes_Test()
                {
                    Assert.True(true);
                }
            }
            """;

        await fixture.Project.AddFileAsync(
            "tests/Project.Tests/UnitTests.cs",
            unitTestClass);

        // Act
        int status = await RunAsync(fixture, [..testCase.Arguments, "--test"]);

        // Assert
        status.ShouldBe(0);

        var actualSdk = await GetSdkVersionAsync(fixture, globalJson);
        actualSdk.ShouldNotBe(testCase.SdkVersion);

        var appTfms = await GetTargetFrameworksAsync(fixture, appProject);
        var testTfms = await GetTargetFrameworksAsync(fixture, testProject);

        appTfms.ShouldNotBe(string.Join(';', testCase.TargetFrameworks));
        testTfms.ShouldNotBe(string.Join(';', testCase.TargetFrameworks));

        var actualPackages = await GetPackageReferencesAsync(fixture, appProject);

        if (testCase.PackageReferences.Count is 0)
        {
            actualPackages.ShouldBe([]);
        }
        else
        {
            actualPackages.ShouldNotBe(testCase.PackageReferences);

            foreach ((string key, string value) in testCase.PackageReferences)
            {
                actualPackages.ShouldNotContainValueForKey(key, value);
            }
        }

        actualPackages = await GetPackageReferencesAsync(fixture, testProject);

        actualPackages.ShouldBe(testPackages);

        var actualConfig = await File.ReadAllTextAsync(vsconfig);

        var config = JsonDocument.Parse(actualConfig).RootElement;
        config.TryGetProperty("components", out var property).ShouldBeTrue();
        property.ValueKind.ShouldBe(JsonValueKind.Array);

        var components = property.EnumerateArray().Select((p) => p.GetString()).ToArray();
        components.ShouldNotContain($"Microsoft.NetCore.Component.Runtime.{testCase.Channel}");
        components.Where((p) => p?.StartsWith("Microsoft.NetCore.Component.Runtime.", StringComparison.Ordinal) is true).Any().ShouldBeTrue();

        var actualVscode = await File.ReadAllTextAsync(vscode);

        config = JsonDocument.Parse(actualVscode).RootElement;
        config.TryGetProperty("configurations", out property).ShouldBeTrue();
        property.ValueKind.ShouldBe(JsonValueKind.Array);

        var configurations = property.EnumerateArray();
        configurations.Count().ShouldBe(1);
        configurations.First().TryGetProperty("program", out property).ShouldBeTrue();
        property.ValueKind.ShouldBe(JsonValueKind.String);
        property.GetString().ShouldStartWith($"${{workspaceFolder}}/src/Project/bin/Debug/net");
        property.GetString().ShouldNotBe($"${{workspaceFolder}}/src/Project/bin/Debug/net{testCase.Channel}/Project.dll");
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

        string globalJson = await fixture.Project.AddGlobalJsonAsync(sdkVersion);
        string projectFile = await fixture.Project.AddProjectAsync(
            "src/Project/Project.csproj",
            [targetFramework]);

        // Act
        int status = await RunAsync(fixture, [..args, "--test"]);

        // Assert
        status.ShouldBe(0);

        var actualSdk = await GetSdkVersionAsync(fixture, globalJson);
        var actualTfm = await GetTargetFrameworksAsync(fixture, projectFile);
        var actualPackages = await GetPackageReferencesAsync(fixture, projectFile);

        actualSdk.ShouldBe(sdkVersion);
        actualTfm.ShouldBe(targetFramework);
        actualPackages.ShouldBe([]);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Application_Warns_If_Tests_Fail(bool treatWarningsAsErrors, int expected)
    {
        // Arrange
        string sdkVersion = "6.0.100";
        string[] targetFrameworks = ["net6.0"];

        var testPackages = new Dictionary<string, string>()
        {
            ["Microsoft.NET.Test.Sdk"] = "17.9.0",
            ["xunit"] = "2.7.0",
            ["xunit.runner.visualstudio"] = "2.5.7",
        };

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddSolutionAsync("Project.sln");
        await fixture.Project.AddGlobalJsonAsync(sdkVersion);
        await fixture.Project.AddProjectAsync("src/Project/Project.csproj", targetFrameworks);
        await fixture.Project.AddProjectAsync("tests/Project.Tests/Project.Tests.csproj", targetFrameworks, testPackages);

        string unitTestClass =
            """
            using Xunit;

            namespace MyProject.Tests;

            public static class UnitTests
            {
                [Fact]
                public static void Always_Fails_Test()
                {
                    Assert.True(false);
                }
            }
            """;

        await fixture.Project.AddFileAsync("tests/Project.Tests/UnitTests.cs", unitTestClass);

        List<string> args = ["--test"];

        if (treatWarningsAsErrors)
        {
            args.Add("--warnings-as-errors");
        }

        // Act
        int status = await Bumper.RunAsync(
            fixture.Console,
            [fixture.Project.DirectoryName, "--verbose", ..args],
            (builder) => builder.AddXUnit(fixture),
            CancellationToken.None);

        // Assert
        status.ShouldBe(expected);
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
        int actual = await RunAsync(fixture, ["--channel=foo"]);

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
        int actual = await Program.Main([fixture.Project.DirectoryName, ..args]);

        // Assert
        actual.ShouldBe(0);
    }

    [Fact]
    public async Task Application_Returns_Two_If_Cancelled_By_User()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        using var cts = new CancellationTokenSource();

        // Act
        int actual = await Bumper.RunAsync(
            fixture.Console,
            [fixture.Project.DirectoryName, "--verbose"],
            (builder) =>
            {
                cts.Cancel();
                return builder.AddXUnit(fixture);
            },
            cts.Token);

        // Assert
        actual.ShouldBe(2);
    }

    [Fact]
    public async Task Application_Validates_Project_Is_Specified()
    {
        // Arrange
        using var console = new TestConsole();

        // Act
        int actual = await Bumper.RunAsync(
            console,
            [],
            (builder) => builder.AddXUnit(outputHelper),
            CancellationToken.None);

        // Assert
        actual.ShouldBe(1);
    }

    private static async Task<int> RunAsync(UpgraderFixture fixture, IList<string> args)
    {
        static bool LogFilter(string? category, LogLevel level)
        {
            if (category is null)
            {
                return false;
            }

            return !(category.StartsWith("Microsoft", StringComparison.Ordinal) ||
                     category.StartsWith("dotnet", StringComparison.Ordinal) ||
                     category.StartsWith("Polly", StringComparison.Ordinal) ||
                     category.StartsWith("System", StringComparison.Ordinal));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        return await Bumper.RunAsync(
            fixture.Console,
            [fixture.Project.DirectoryName, "--verbose", "--warnings-as-errors", ..args],
            (builder) => builder.AddXUnit(fixture).AddFilter(LogFilter),
            cts.Token);
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

    private static async Task<Dictionary<string, string>> GetPackageReferencesAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var xml = await fixture.Project.GetFileAsync(fileName);
        var project = XDocument.Parse(xml);

        return project
            .Root?
            .Elements("ItemGroup")
            .Elements("PackageReference")
            .Select((p) =>
                new
                {
                    Key = p.Attribute("Include")?.Value ?? string.Empty,
                    Value = p.Attribute("Version")?.Value ?? string.Empty,
                })
            .ToDictionary((p) => p.Key, (p) => p.Value) ?? [];
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

    private static Dictionary<string, string> Packages(params (string Name, string Version)[] packages)
        => packages.ToDictionary((p) => p.Name, (p) => p.Version);
}
