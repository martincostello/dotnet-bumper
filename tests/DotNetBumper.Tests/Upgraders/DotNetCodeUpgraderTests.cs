// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class DotNetCodeUpgraderTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<string> Channels()
    {
#pragma warning disable IDE0028 // See https://github.com/dotnet/roslyn/issues/72668
        return new()
        {
            "7.0",
            //// "8.0", See https://github.com/dotnet/sdk/issues/39742
            //// "9.0", See https://github.com/dotnet/sdk/issues/39909 and https://github.com/dotnet/sdk/issues/40174
        };
#pragma warning restore IDE0028
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task UpgradeAsync_Applies_Code_Fix(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade);

        string code =
            """
            namespace Project;

            /// <summary>
            /// A person.
            /// </summary>
            public class Person
            {
                /// <summary>Gets or sets the name of the person.</summary>
                public string Name { get; set; } = string.Empty;

                /// <summary>Truncates the name to the specified length.</summary>
                /// <param name="length">The length to truncate the name to.</param>
                /// <returns>The truncated name.</returns>
                public string TruncateName(int length) => Name.Substring(0, length); // IDE0057
            }
            """;

        await fixture.Project.AddFileAsync("src/Project/Person.cs", code);

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
        fixture.LogContext.Changelog.ShouldContain("Fix IDE0057 warning");

        // Act
        actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task UpgradeAsync_Applies_Code_Fixes(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade);

        string code =
            """
            namespace Project;

            /// <summary>
            /// A person.
            /// </summary>
            public class Person
            {
                /// <summary>Gets or sets the name of the person.</summary>
                public string Name { get; set; } = string.Empty;

                /// <summary>Truncates the name to the specified length.</summary>
                /// <param name="length">The length to truncate the name to.</param>
                /// <returns>The truncated name.</returns>
                public string TruncateName(int length) => Name.Substring(0, length); // IDE0057

                /// <summary>Gets the name's 3 character prefix.</summary>
                /// <returns>The prefix of the name.</returns>
                public string NamePrefix() => Name.Substring(0, 3); // IDE0057
            }
            """;

        await fixture.Project.AddFileAsync("src/Project/Person.cs", code);

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
        fixture.LogContext.Changelog.ShouldContain("Fix IDE0057 warnings");

        // Act
        actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task UpgradeAsync_Honors_User_Project_Settings(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade, noWarn: "IDE0057");

        string code =
            """
            namespace Project;

            /// <summary>
            /// A person.
            /// </summary>
            public class Person
            {
                /// <summary>Gets or sets the name of the person.</summary>
                public string Name { get; set; } = string.Empty;

                /// <summary>Truncates the name to the specified length.</summary>
                /// <param name="length">The length to truncate the name to.</param>
                /// <returns>The truncated name.</returns>
                public string TruncateName(int length) => Name.Substring(0, length); // IDE0057
            }
            """;

        await fixture.Project.AddFileAsync("src/Project/Person.cs", code);

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task UpgradeAsync_Does_Not_Fix_Information_Diagnostics(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

        using var fixture = await CreateFixtureAsync(upgrade, severity: "info");

        string code =
            """
            namespace Project;

            /// <summary>
            /// A person.
            /// </summary>
            public class Person
            {
                /// <summary>Gets or sets the name of the person.</summary>
                public string Name { get; set; } = string.Empty;

                /// <summary>Truncates the name to the specified length.</summary>
                /// <param name="length">The length to truncate the name to.</param>
                /// <returns>The truncated name.</returns>
                public string TruncateName(int length) => Name.Substring(0, length); // IDE0057
            }
            """;

        await fixture.Project.AddFileAsync("src/Project/Person.cs", code);

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    private static DotNetCodeUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            new(fixture.CreateLogger<DotNetProcess>()),
            fixture.Console,
            fixture.Environment,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<DotNetCodeUpgrader>());
    }

    private async Task<UpgraderFixture> CreateFixtureAsync(
        UpgradeInfo upgrade,
        string severity = "warning",
        string? noWarn = null)
    {
        string editorconfig =
            $$"""
              root = true
              
              [*]
              end_of_line = lf
              indent_size = 4
              indent_style = space
              insert_final_newline = true
              trim_trailing_whitespace = true
              
              [*.{cs,vb}]
              dotnet_analyzer_diagnostic.category-Style.severity = {{severity}}
              
              [*.cs]
              csharp_style_expression_bodied_methods = when_on_single_line
              csharp_style_namespace_declarations = file_scoped
              """;

        var fixture = new UpgraderFixture(outputHelper);

        try
        {
            await fixture.Project.AddApplicationProjectAsync([$"net{upgrade.Channel}"]);
            await fixture.Project.AddDirectoryBuildPropsAsync(noWarn: noWarn);
            await fixture.Project.AddEditorConfigAsync(editorconfig);
            await fixture.Project.AddGlobalJsonAsync(upgrade.SdkVersion.ToString());

            return fixture;
        }
        catch (Exception)
        {
            fixture.Dispose();
            throw;
        }
    }

    private async Task<UpgradeInfo> GetUpgradeAsync(string channel)
    {
        // Use the same SDK version as the upgrade to prevent a different dotnet format version being used
        var finder = new DotNetUpgradeFinder(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new UpgradeOptions() { DotNetChannel = channel }),
            outputHelper.ToLogger<DotNetUpgradeFinder>());

        var upgrade = await finder.GetUpgradeAsync(CancellationToken.None);
        upgrade.ShouldNotBeNull();

        return upgrade;
    }
}
