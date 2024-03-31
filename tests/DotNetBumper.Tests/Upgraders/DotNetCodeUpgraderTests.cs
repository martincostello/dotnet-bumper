// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class DotNetCodeUpgraderTests(ITestOutputHelper outputHelper)
{
#pragma warning disable IDE0028 // See https://github.com/dotnet/roslyn/issues/72668
    public static TheoryData<string> Channels()
    {
        return new()
        {
            "7.0",
            "8.0",
            //// "9.0", See https://github.com/dotnet/sdk/issues/39909
        };
#pragma warning restore IDE0028
    }

    [Theory]
    [MemberData(nameof(Channels))]
    public async Task UpgradeAsync_Applies_Code_Fix(string channel)
    {
        // Arrange
        var upgrade = await GetUpgradeAsync(channel);

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

        string editorconfig =
            """
            root = true
            
            [*]
            end_of_line = lf
            indent_size = 4
            indent_style = space
            insert_final_newline = true
            trim_trailing_whitespace = true
            
            [*.{cs,vb}]
            dotnet_analyzer_diagnostic.category-Style.severity = warning
            
            [*.cs]
            csharp_style_expression_bodied_methods = when_on_single_line
            csharp_style_namespace_declarations = file_scoped
            """;

        string properties =
            """
            <Project>
              <PropertyGroup>
                <AnalysisMode>All</AnalysisMode>
                <EnableNETAnalyzers>true</EnableNETAnalyzers>
                <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
                <GenerateDocumentationFile>true</GenerateDocumentationFile>
              </PropertyGroup>
            </Project>
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddApplicationProjectAsync([$"net{channel}"]);
        await fixture.Project.AddDirectoryBuildPropsAsync(properties);
        await fixture.Project.AddEditorConfigAsync(editorconfig);
        await fixture.Project.AddFileAsync("src/Project/Person.cs", code);
        await fixture.Project.AddGlobalJsonAsync(upgrade.SdkVersion.ToString());

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);
        fixture.LogContext.Changelog.ShouldContain("Fix IDE0057 warning");

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
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
