// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public class BumperConfigurationProviderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData(UpgradeType.Latest)]
    [InlineData(UpgradeType.Lts)]
    public async Task GetAsync_When_No_Custom_Configuration_For_Stable(UpgradeType upgradeType)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var target = CreateTarget(fixture, upgradeType);

        // Act
        var actual = await target.GetAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe([]);
        actual.IncludeNuGetPackages.ShouldBe(["Aspire.", "Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.", "System.Text.Json", "Microsoft.NET.Test.Sdk"]);
        actual.NoWarn.ShouldBe(["NU1605"]);
        actual.RemainingReferencesIgnore.ShouldBe([]);
    }

    [Fact]
    public async Task GetAsync_When_No_Custom_Configuration_For_Preview()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var target = CreateTarget(fixture, UpgradeType.Preview);

        // Act
        var actual = await target.GetAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe([]);
        actual.IncludeNuGetPackages.ShouldBe(["Aspire.", "Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.", "System.Text.Json"]);
        actual.NoWarn.ShouldBe(["NETSDK1057", "NU5104", "NU1605"]);
        actual.RemainingReferencesIgnore.ShouldBe([]);
    }

    [Fact]
    public async Task GetAsync_When_Json_Configuration()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await BumperConfigurationLoaderTests.CreateJsonConfigurationAsync(fixture);

        var target = CreateTarget(fixture, UpgradeType.Preview);

        // Act
        var actual = await target.GetAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe(["exclude-json-1", "exclude-json-2"]);
        actual.IncludeNuGetPackages.ShouldBe(["Aspire.", "Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.", "System.Text.Json", "include-json-1", "include-json-2"]);
        actual.NoWarn.ShouldBe(["NETSDK1057", "NU5104", "NU1605", "no-warn-json-1", "no-warn-json-2"]);
        actual.RemainingReferencesIgnore.ShouldBe(["ignore-json-1", "ignore-json-2"]);
    }

    [Fact]
    public async Task GetAsync_When_Yaml_Configuration()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        await BumperConfigurationLoaderTests.CreateYamlConfigurationAsync(fixture);

        var target = CreateTarget(fixture, UpgradeType.Preview);

        // Act
        var actual = await target.GetAsync(CancellationToken.None);

        // Assert
        actual.ShouldNotBeNull();
        actual.ExcludeNuGetPackages.ShouldBe(["exclude-yaml-1", "exclude-yaml-2"]);
        actual.IncludeNuGetPackages.ShouldBe(["Aspire.", "Microsoft.AspNetCore.", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions.", "System.Text.Json", "include-yaml-1", "include-yaml-2"]);
        actual.NoWarn.ShouldBe(["NETSDK1057", "NU5104", "NU1605", "no-warn-yaml-1", "no-warn-yaml-2"]);
        actual.RemainingReferencesIgnore.ShouldBe(["ignore-yaml-1", "ignore-yaml-2"]);
    }

    private static BumperConfigurationProvider CreateTarget(UpgraderFixture fixture, UpgradeType? upgradeType = null)
    {
        var options = fixture.CreateOptions();

        if (upgradeType is { } value)
        {
            options.Value.UpgradeType = value;
        }

        var loader = new BumperConfigurationLoader(options, fixture.CreateLogger<BumperConfigurationLoader>());

        return new(loader, options, fixture.CreateLogger<BumperConfigurationProvider>());
    }
}
