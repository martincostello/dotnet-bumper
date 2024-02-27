// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

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
    [InlineData(".dotnet-bumper.json", "Not JSON")]
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

    private static BumperConfigurationLoader CreateTarget(UpgraderFixture fixture)
    {
        return new(fixture.CreateOptions(), fixture.CreateLogger<BumperConfigurationLoader>());
    }
}
