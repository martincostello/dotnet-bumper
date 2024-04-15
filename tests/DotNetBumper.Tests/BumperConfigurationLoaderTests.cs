// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper;

public class BumperConfigurationLoaderTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task LoadAsync_When_No_Custom_Configuration()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_When_Custom_Configuration_Not_Found()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await CreateJsonConfigurationAsync(fixture);

        var options = new UpgradeOptions()
        {
            ConfigurationPath = Path.Combine(fixture.Project.DirectoryName, "custom.json"),
            ProjectPath = fixture.Project.DirectoryName,
        };

        var target = CreateTarget(fixture, options);

        // Act and Assert
        await Should.ThrowAsync<FileNotFoundException>(() => target.LoadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("custom.json")]
    [InlineData("custom.JSON")]
    [InlineData("custom.yml")]
    [InlineData("custom.YML")]
    [InlineData("custom.yaml")]
    public async Task LoadAsync_When_Custom_Configuration_Invalid(string fileName)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await CreateJsonConfigurationAsync(fixture);

        var configurationPath = await fixture.Project.AddFileAsync(fileName, "Invalid");

        var options = new UpgradeOptions()
        {
            ConfigurationPath = configurationPath,
            ProjectPath = fixture.Project.DirectoryName,
        };

        var target = CreateTarget(fixture, options);

        // Act and Assert
        await Should.ThrowAsync<InvalidOperationException>(() => target.LoadAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("custom.json", /*lang=json,strict*/ @"{""noWarn"":[""warning-42""]}")]
    [InlineData("CUSTOM.JSON", /*lang=json,strict*/ @"{""noWarn"":[""warning-42""]}")]
    [InlineData("custom.yml", "noWarn:\n- warning-42")]
    [InlineData("custom.YML", "noWarn:\n- warning-42")]
    public async Task LoadAsync_When_Custom_Configuration(string fileName, string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var configurationPath = await fixture.Project.AddFileAsync(fileName, content);

        var options = new UpgradeOptions()
        {
            ConfigurationPath = configurationPath,
            ProjectPath = fixture.Project.DirectoryName,
        };

        var target = CreateTarget(fixture, options);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.NoWarn.ShouldBe(["warning-42"]);
    }

    [Fact]
    public async Task LoadAsync_When_Json_Configuration()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await CreateJsonConfigurationAsync(fixture);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe(["exclude-json-1", "exclude-json-2"]);
        actual.IncludeNuGetPackages.ShouldBe(["include-json-1", "include-json-2"]);
        actual.NoWarn.ShouldBe(["no-warn-json-1", "no-warn-json-2"]);
        actual.RemainingReferencesIgnore.ShouldBe(["ignore-json-1", "ignore-json-2"]);
    }

    [Theory]
    [InlineData("yml")]
    [InlineData("yaml")]
    public async Task LoadAsync_When_Yaml_Configuration(string extension)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await CreateYamlConfigurationAsync(fixture, extension);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe(["exclude-yaml-1", "exclude-yaml-2"]);
        actual.IncludeNuGetPackages.ShouldBe(["include-yaml-1", "include-yaml-2"]);
        actual.NoWarn.ShouldBe(["no-warn-yaml-1", "no-warn-yaml-2"]);
        actual.RemainingReferencesIgnore.ShouldBe(["ignore-yaml-1", "ignore-yaml-2"]);
    }

    [Fact]
    public async Task LoadAsync_When_Json_And_Yaml_Configuration()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await CreateJsonConfigurationAsync(fixture);
        await CreateYamlConfigurationAsync(fixture);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe(["exclude-json-1", "exclude-json-2"]);
        actual.IncludeNuGetPackages.ShouldBe(["include-json-1", "include-json-2"]);
        actual.NoWarn.ShouldBe(["no-warn-json-1", "no-warn-json-2"]);
        actual.RemainingReferencesIgnore.ShouldBe(["ignore-json-1", "ignore-json-2"]);
    }

    [Theory]
    [InlineData(".dotnet-bumper.json", "<NotJson/>")]
    [InlineData(".dotnet-bumper.yml", "Not YAML")]
    public async Task LoadAsync_Handles_Invalid_Content(string fileName, string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await File.WriteAllTextAsync(Path.Combine(fixture.Project.DirectoryName, fileName), content);

        var target = CreateTarget(fixture);

        // Act
        var actual = await target.LoadAsync(CancellationToken.None);

        // Assert
        actual.ShouldBeNull();
    }

    internal static async Task CreateJsonConfigurationAsync(UpgraderFixture fixture)
    {
        var path = Path.Combine(fixture.Project.DirectoryName, ".dotnet-bumper.json");

        /*lang=json*/
        string content =
            """
            {
              /* Custom JSON configuration */
              "excludeNuGetPackages": [
                "exclude-json-1",
                "exclude-json-2",
              ],
              "includeNuGetPackages": [
                "include-json-1",
                "include-json-2",
              ],
              "noWarn": [
                "no-warn-json-1",
                "no-warn-json-2",
              ],
              "remainingReferencesIgnore": [
                "ignore-json-1",
                "ignore-json-2",
              ],
            }
            """;

        await File.WriteAllTextAsync(path, content);
    }

    internal static async Task CreateYamlConfigurationAsync(UpgraderFixture fixture, string extension = "yml")
    {
        var path = Path.Combine(fixture.Project.DirectoryName, $".dotnet-bumper.{extension}");

        string content =
            """
            # Custom YAML configuration
            excludeNuGetPackages:
              - exclude-yaml-1
              - exclude-yaml-2
            includeNuGetPackages:
              - include-yaml-1
              - include-yaml-2
            noWarn:
              - no-warn-yaml-1
              - no-warn-yaml-2
            remainingReferencesIgnore:
              - ignore-yaml-1
              - ignore-yaml-2
            """;

        await File.WriteAllTextAsync(path, content);
    }

    private static BumperConfigurationLoader CreateTarget(UpgraderFixture fixture, UpgradeOptions? options = null)
    {
        var fixtureOptions = options is not null ? Options.Create(options) : fixture.CreateOptions();
        return new(fixtureOptions, fixture.CreateLogger<BumperConfigurationLoader>());
    }
}
