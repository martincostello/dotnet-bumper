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

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, "build.ps1", channel);
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
        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, "script.ps1", "10.0");
    }

    [Fact]
    public async Task UpgradeAsync_Upgrades_PowerShell_Script_Embedded_In_Actions_Workflow_One_Line()
    {
        // Arrange
        string fileContents =
            """
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
            """
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

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
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

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Fact]
    public async Task UpgradeAsync_Upgrades_PowerShell_Scripts_Embedded_In_Actions_Workflow()
    {
        // Arrange
        string fileContents =
            """
            name: build

            on: [push]

            jobs:

              build:
                runs-on: ubuntu-latest
                steps:

                  - name: Install .NET
                    run: |
                      declare repo_version=$(if command -v lsb_release &> /dev/null; then lsb_release -r -s; else grep -oP '(?<=^VERSION_ID=).+' /etc/os-release | tr -d '"'; fi)
                      wget https://packages.microsoft.com/config/ubuntu/$repo_version/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
                      sudo dpkg -i packages-microsoft-prod.deb
                      rm packages-microsoft-prod.deb
                      sudo apt update
                      sudo apt install dotnet-sdk-7.0

                  - name: Checkout code
                    uses: actions/checkout@v4

                  - name: Build and publish
                    shell: pwsh
                    run: |
                      dotnet build --framework net7.0 --output ./artifacts/build
                      dotnet publish --framework net7.0 --output ./artifacts/publish --runtime win10-x64

                  - name: Run tests
                    shell: pwsh
                    run: dotnet test --framework net7.0

                  - name: Publish artifacts
                    uses: actions/upload-artifact@v4
                    with:
                      name: webapp
                      path: ./artifacts/publish
            """;

        string expectedContents =
            """
            name: build

            on: [push]

            jobs:

              build:
                runs-on: ubuntu-latest
                steps:

                  - name: Install .NET
                    run: |
                      declare repo_version=$(if command -v lsb_release &> /dev/null; then lsb_release -r -s; else grep -oP '(?<=^VERSION_ID=).+' /etc/os-release | tr -d '"'; fi)
                      wget https://packages.microsoft.com/config/ubuntu/$repo_version/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
                      sudo dpkg -i packages-microsoft-prod.deb
                      rm packages-microsoft-prod.deb
                      sudo apt update
                      sudo apt install dotnet-sdk-7.0

                  - name: Checkout code
                    uses: actions/checkout@v4

                  - name: Build and publish
                    shell: pwsh
                    run: |
                      dotnet build --framework net8.0 --output ./artifacts/build
                      dotnet publish --framework net8.0 --output ./artifacts/publish --runtime win-x64

                  - name: Run tests
                    shell: pwsh
                    run: dotnet test --framework net8.0

                  - name: Publish artifacts
                    uses: actions/upload-artifact@v4
                    with:
                      name: webapp
                      path: ./artifacts/publish
            """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Fact]
    public async Task UpgradeAsync_Updates_Actions_Workflows_With_Bash()
    {
        // Arrange
        string fileContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - shell: bash
                    run: dotnet publish --framework net6.0
            """;

        string expectedContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - shell: bash
                    run: dotnet publish --framework net8.0
            """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Fact]
    public async Task UpgradeAsync_Ignores_Actions_Workflows_With_No_PowerShell()
    {
        // Arrange
        string fileContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - shell: python
                    run: dotnet publish --framework net6.0
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Updates_Actions_Workflows_With_No_Explicit_Shell()
    {
        // Arrange
        string fileContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - run: dotnet publish --framework net6.0
            """;

        string expectedContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - run: dotnet publish --framework net8.0
            """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Fact]
    public async Task UpgradeAsync_Updates_Actions_Workflows_With_GitHub_Actions_Workflow_Syntax()
    {
        // Arrange
        string fileContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - run: dotnet publish --framework net6.0 --configuration "${{ env.MY_VARIABLE }}"
            """;

        string expectedContents =
            """
            name: build
            on: [push]
            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v3
                  - run: dotnet publish --framework net8.0 --configuration "${{ env.MY_VARIABLE }}"
            """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("sh")]
    public async Task UpgradeAsync_Updates_Actions_Workflows_With_GitHub_Actions_Workflow_Syntax_And_Explicit_Shell(string shell)
    {
        // Arrange
        string fileContents =
            $$$"""
               name: build
               on: [push]
               jobs:
                 build:
                   runs-on: ubuntu-latest
                   steps:
                     - uses: actions/checkout@v4
                     - uses: actions/setup-dotnet@v3
                     - run: dotnet publish --framework net6.0 --configuration "${{ env.MY_VARIABLE }}"
                       shell: {{{shell}}}
               """;

        string expectedContents =
            $$$"""
               name: build
               on: [push]
               jobs:
                 build:
                   runs-on: ubuntu-latest
                   steps:
                     - uses: actions/checkout@v4
                     - uses: actions/setup-dotnet@v3
                     - run: dotnet publish --framework net8.0 --configuration "${{ env.MY_VARIABLE }}"
                       shell: {{{shell}}}
               """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, ".github/workflows/build.yml", "8.0");
    }

    [Fact]
    public async Task UpgradeAsync_Handles_Invalid_Workflow_Yaml()
    {
        // Arrange
        string fileContents =
            """
            foo: bar
            baz
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

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

        await fixture.Project.AddFileAsync("script.ps1", content);

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
        ProcessingResult actual = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(script, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(script, fixture.CancellationToken);

        if (hasUtf8Bom)
        {
            actualBytes.ShouldStartWithUTF8Bom();
        }
        else
        {
            actualBytes.ShouldNotStartWithUTF8Bom();
        }

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Batch_Script(string channel)
    {
        // Arrange
        string fileContents =
            """
            @ECHO OFF
            SETLOCAL

            :: This tells .NET to use the same dotnet.exe that the build script uses.
            SET DOTNET_ROOT=%~dp0.dotnetcli
            SET DOTNET_ROOT(x86)=%~dp0.dotnetcli\x86

            dotnet build -f "net6.0"

            exit /b 1
            """;

        string expectedContents =
            $$"""
             @ECHO OFF
             SETLOCAL
             
             :: This tells .NET to use the same dotnet.exe that the build script uses.
             SET DOTNET_ROOT=%~dp0.dotnetcli
             SET DOTNET_ROOT(x86)=%~dp0.dotnetcli\x86
             
             dotnet build -f "net{{channel}}"
             
             exit /b 1
             """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, "build.cmd", channel);
    }

    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Shell_Script(string channel)
    {
        // Arrange
        string fileContents =
            """
            #!/usr/bin/env bash

            root=$(cd "$(dirname "$0")"; pwd -P)
            artifacts=$root/artifacts
            configuration=Release
            skipTests=0

            RED='\033[0;31m'
            GREEN='\033[0;32m'
            NC='\033[0m'

            while :; do
                if [ $# -le 0 ]; then
                    break
                fi

                lowerI="$(echo $1 | awk '{print tolower($0)}')"
                case $lowerI in
                    -\?|-h|--help)
                        echo "./build.sh [--skip-tests]"
                        exit 1
                        ;;

                    --skip-tests)
                        skipTests=1
                        ;;

                    *)
                        __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
                        ;;
                esac
                shift
            done

            export CLI_VERSION=`cat ./global.json | grep -E '[0-9]\.[0-9]\.[a-zA-Z0-9\-]*' -o`
            dotnet_version=$(dotnet --version)

            printf "${GREEN}Installed .NET SDK Version: $dotnet_version. Required version: $CLI_VERSION.${NC}\n"
            echo "Output path: $artifacts."
            printf "${GREEN}BUILD NUMBER=$BUILD_NUMBER ${NC}\n"

            if [ "$skipTests" == "0" ]; then
                tests_failed=0
                printf "${GREEN}Executing tests...${NC}\n"
                dotnet test tests/Project.Tests --configuration $configuration --framework net6.0 || tests_failed=1
                if [ "$tests_failed" == "1" ]; then
                    printf "${RED}Tests failed.${NC}\n"
                    exit 1
                fi
            fi

            printf "${GREEN}Publishing application ${NC}\n"
            dotnet publish "./src/Project" --output "$artifacts"  --configuration $configuration --framework net6.0 || exit 1
            """;

        string expectedContents =
            $$"""
             #!/usr/bin/env bash
             
             root=$(cd "$(dirname "$0")"; pwd -P)
             artifacts=$root/artifacts
             configuration=Release
             skipTests=0
             
             RED='\033[0;31m'
             GREEN='\033[0;32m'
             NC='\033[0m'
             
             while :; do
                 if [ $# -le 0 ]; then
                     break
                 fi
             
                 lowerI="$(echo $1 | awk '{print tolower($0)}')"
                 case $lowerI in
                     -\?|-h|--help)
                         echo "./build.sh [--skip-tests]"
                         exit 1
                         ;;
             
                     --skip-tests)
                         skipTests=1
                         ;;
             
                     *)
                         __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
                         ;;
                 esac
                 shift
             done
             
             export CLI_VERSION=`cat ./global.json | grep -E '[0-9]\.[0-9]\.[a-zA-Z0-9\-]*' -o`
             dotnet_version=$(dotnet --version)
             
             printf "${GREEN}Installed .NET SDK Version: $dotnet_version. Required version: $CLI_VERSION.${NC}\n"
             echo "Output path: $artifacts."
             printf "${GREEN}BUILD NUMBER=$BUILD_NUMBER ${NC}\n"
             
             if [ "$skipTests" == "0" ]; then
                 tests_failed=0
                 printf "${GREEN}Executing tests...${NC}\n"
                 dotnet test tests/Project.Tests --configuration $configuration --framework net{{channel}} || tests_failed=1
                 if [ "$tests_failed" == "1" ]; then
                     printf "${RED}Tests failed.${NC}\n"
                     exit 1
                 fi
             fi
             
             printf "${GREEN}Publishing application ${NC}\n"
             dotnet publish "./src/Project" --output "$artifacts"  --configuration $configuration --framework net{{channel}} || exit 1
             """;

        // Act and Assert
        await AssertUpgradedAsync(fileContents, expectedContents, "build.sh", channel);
    }

    private static PowerShellScriptUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<PowerShellScriptUpgrader>());
    }

    private async Task AssertUpgradedAsync(
        string fileContents,
        string expectedContents,
        string fileName,
        string channel)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string script = await fixture.Project.AddFileAsync(fileName, fileContents);

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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(script);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }
}
