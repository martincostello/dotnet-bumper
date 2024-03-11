// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class PowerShellScriptUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_PowerShell_Script(string channel)
    {
        // Arrange
        string fileContents =
            """
            #! /usr/bin/env pwsh

            #Requires -PSEdition Core
            #Requires -Version 7

            param(
                [Parameter(Mandatory = $false)][switch] $SkipTests,
                [Parameter(Mandatory = $false)][string] $Configuration = "Release",
                [Parameter(Mandatory = $false)][string] $Framework = "net6.0",
                [Parameter(Mandatory = $false)][string] $Runtime = "win10-x64"
            )

            $ErrorActionPreference = "Stop"
            $ProgressPreference = "SilentlyContinue"

            dotnet publish --configuration $Configuration --framework $Framework --runtime $Runtime
            """;

        string expectedContents =
            $"""
             #! /usr/bin/env pwsh
             
             #Requires -PSEdition Core
             #Requires -Version 7
             
             param(
                 [Parameter(Mandatory = $false)][switch] $SkipTests,
                 [Parameter(Mandatory = $false)][string] $Configuration = "Release",
                 [Parameter(Mandatory = $false)][string] $Framework = "net{channel}",
                 [Parameter(Mandatory = $false)][string] $Runtime = "win-x64"
             )
             
             $ErrorActionPreference = "Stop"
             $ProgressPreference = "SilentlyContinue"
             
             dotnet publish --configuration $Configuration --framework $Framework --runtime $Runtime
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string script = await fixture.Project.AddFileAsync("build.ps1", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(script);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("dotnet publish --framework net6.0", "dotnet publish --framework net10.0")]
    [InlineData("dotnet publish --framework 'net6.0'", "dotnet publish --framework 'net10.0'")]
    [InlineData("dotnet publish --framework \"net6.0\"", "dotnet publish --framework \"net10.0\"")]
    [InlineData("dotnet publish `\n       --framework \"net6.0\"", "dotnet publish `\n       --framework \"net10.0\"")]
    [InlineData("dotnet publish --runtime win10-x64", "dotnet publish --runtime win-x64")]
    [InlineData("dotnet publish --runtime win10-x64 # A comment", "dotnet publish --runtime win-x64 # A comment")]
    public async Task UpgradeAsync_Upgrades_PowerShell_Script_Correctly(string fileContents, string expectedContents)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string script = await fixture.Project.AddFileAsync("script.ps1", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(10, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(script);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Upgrades_PowerShell_Script_Embedded_In_Actions_Workflow_One_Line()
    {
        // Arrange
        string fileContents =
            $"""
             name: build
             on: [push]
             jobs:
               build:               
                 runs-on: ubuntu-latest             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v3
                   - shell: pwsh
                     run: dotnet publish --framework net6.0
             """;

        string expectedContents =
            $"""
             name: build
             on: [push]
             jobs:
               build:               
                 runs-on: ubuntu-latest             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v3
                   - shell: pwsh
                     run: dotnet publish --framework net8.0
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(workflow);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData('|')]
    [InlineData('>')]
    public async Task UpgradeAsync_Upgrades_PowerShell_Script_Embedded_In_Actions_Workflow_Multiline(char blockCharacter)
    {
        // Arrange
        string fileContents =
            $"""
             name: build
             on: [push]
             jobs:
               build:               
                 runs-on: ubuntu-latest             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v3
                   - shell: pwsh
                     run: {blockCharacter}
                       dotnet publish --framework net6.0
             """;

        string expectedContents =
            $"""
             name: build
             on: [push]
             jobs:
               build:               
                 runs-on: ubuntu-latest             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v3
                   - shell: pwsh
                     run: {blockCharacter}
                       dotnet publish --framework net8.0
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(workflow);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("$foo = @(1 2)")]
    public async Task UpgradeAsync_Handles_Invalid_Scripts(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string script = await fixture.Project.AddFileAsync("script.ps1", content);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.201"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actual = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actual.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [ClassData(typeof(FileEncodingTestData))]
    public async Task UpgradeAsync_Preserves_Line_Endings(string newLine, bool hasUtf8Bom)
    {
        // Arrange
        string[] originalLines =
        [
            "#! /usr/bin/env pwsh",
            string.Empty,
            "dotnet publish --framework \"net6.0\" --runtime \"win10-x64\"",
        ];

        string[] expectedLines =
        [
            "#! /usr/bin/env pwsh",
            string.Empty,
            "dotnet publish --framework \"net10.0\" --runtime \"win-x64\"",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string script = await fixture.Project.AddFileAsync("script.ps1", fileContents, encoding);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("10.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("10.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        var target = CreateTarget(fixture);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(script);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(script);

        if (hasUtf8Bom)
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

    private static PowerShellScriptUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<PowerShellScriptUpgrader>());
    }
}
