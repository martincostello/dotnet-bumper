// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

public class GitHubActionsUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action(string channel)
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: 6.0

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: {channel}
             
                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yaml", fileContents);

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

        string actualContent = await File.ReadAllTextAsync(workflow);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("10.0", "10.0.100", "6", "10")]
    [InlineData("10.0", "10.0.100", "6.x", "10.x")]
    [InlineData("10.0", "10.0.100", "6.0", "10.0")]
    [InlineData("10.0", "10.0.100", "6.0.x", "10.0.x")]
    [InlineData("10.0", "10.0.200", "6.0.4xx", "10.0.2xx")]
    [InlineData("10.0", "10.0.200", "6.0.422", "10.0.200")]
    [InlineData("10.0", "10.0.200", "10.0.1xx", "10.0.2xx")]
    [InlineData("10.0", "10.0.200", "10.0.100", "10.0.200")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6", "10")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6.x", "10.x")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6.0", "10.0")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6.0.x", "10.0.x")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6.0.4xx", "10.0.1xx")]
    [InlineData("10.0", "10.0.100-preview.1.2", "6.0.422", "10.0.100-preview.1.2")]
    [InlineData("10.0", "10.0.100-preview.2.3", "10.0.100-preview.1.2", "10.0.100-preview.2.3")]
    [InlineData("10.0", "10.0.100", "10.0.100-preview.1.2", "10.0.100")]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action_Sdk_Version(
        string channel,
        string sdkVersion,
        string version,
        string expected)
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: {version} # Pin the version

                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: {expected} # Pin the version

                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new(sdkVersion),
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
    [InlineData("9.0")]
    [InlineData("10.0")]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action_With_Single_Sdk_Version(string channel)
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        6.0.x

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish

              test:
                runs-on: ubuntu-latest

                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        6.0.x

                  - name: Test app
                    run: dotnet test
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         {channel}.x

                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish

               test:
                 runs-on: ubuntu-latest
             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         {channel}.x

                   - name: Test app
                     run: dotnet test
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.200"),
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

    [Fact]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action_With_Single_Preview_Sdk_Version()
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        9.0.100-preview.1.2

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish

              test:
                runs-on: ubuntu-latest

                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        9.0.100-preview.1.2

                  - name: Test app
                    run: dotnet test
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        9.0.100-preview.2.3

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish

              test:
                runs-on: ubuntu-latest
            
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        9.0.100-preview.2.3

                  - name: Test app
                    run: dotnet test
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("9.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("9.0.100-preview.2.3"),
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
    [InlineData("9.0", "9.0.100", "", "9")]
    [InlineData("9.0", "9.0.100", ".0", "9.0")]
    [InlineData("9.0", "9.0.100", ".x", "9.x")]
    [InlineData("9.0", "9.0.100", ".0.x", "9.0.x")]
    [InlineData("9.0", "9.0.100", ".0.123", "9.0.100")]
    [InlineData("9.0", "9.0.100", ".0.4xx", "9.0.1xx")]
    [InlineData("9.0", "9.0.200", ".0.423", "9.0.200")]
    [InlineData("9.0", "9.0.234", ".0.423", "9.0.234")]
    [InlineData("9.0", "9.0.200", ".0.4xx", "9.0.2xx")]
    [InlineData("9.0", "9.0.100-preview.1.2", ".0.4xx", "9.0.1xx")]
    [InlineData("10.0", "10.0.100", "", "10")]
    [InlineData("10.0", "10.0.100", ".0", "10.0")]
    [InlineData("10.0", "10.0.100", ".x", "10.x")]
    [InlineData("10.0", "10.0.100", ".0.x", "10.0.x")]
    [InlineData("10.0", "10.0.100", ".0.123", "10.0.100")]
    [InlineData("10.0", "10.0.100", ".0.4xx", "10.0.1xx")]
    [InlineData("10.0", "10.0.200", ".0.423", "10.0.200")]
    [InlineData("10.0", "10.0.234", ".0.423", "10.0.234")]
    [InlineData("10.0", "10.0.200", ".0.4xx", "10.0.2xx")]
    [InlineData("10.0", "10.0.100-preview.1.2", ".0.4xx", "10.0.1xx")]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action_With_Multiple_Sdk_Versions(
        string channel,
        string sdkVersion,
        string suffix,
        string expected)
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         6{suffix}
                         8{suffix}

                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish
             
               test:
                 runs-on: ubuntu-latest
             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         6{suffix}
                         8{suffix}

                   - name: Test app
                     run: dotnet test
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
                   - name: Setup .NET
                     uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         6{suffix}
                         8{suffix}
                         {expected}

                   - name: Publish app
                     shell: pwsh
                     run: dotnet publish

               test:
                 runs-on: ubuntu-latest
             
                 steps:
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: |
                         6{suffix}
                         8{suffix}
                         {expected}

                   - name: Test app
                     run: dotnet test
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new(sdkVersion),
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

    [Fact]
    public async Task UpgradeAsync_Upgrades_Setup_DotNet_Action_From_Preview_Sdk_Version_To_Stable()
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        8.0
                        9.0.100-preview.1.2

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish

              test:
                runs-on: ubuntu-latest

                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        8.0
                        9.0.100-preview.1.2

                  - name: Test app
                    run: dotnet test
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
                  - name: Setup .NET
                    uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        8.0
                        9.0

                  - name: Publish app
                    shell: pwsh
                    run: dotnet publish

              test:
                runs-on: ubuntu-latest
            
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: |
                        8.0
                        9.0

                  - name: Test app
                    run: dotnet test
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yml", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse("9.0"),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("9.0.100"),
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

    [Fact]
    public async Task UpgradeAsync_Ignores_Actions_Workflows_With_No_Setup_DotNet_Action()
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
                  - uses: actions/setup-node@v4
                  - run: npm ci && npm test
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Fact]
    public async Task UpgradeAsync_Ignores_Actions_Workflows_With_No_Steps()
    {
        // Arrange
        string fileContents =
            """
            name: update-dotnet-sdk

            on:
              schedule:
                - cron:  '00 20 * * TUE'
              workflow_dispatch:

            permissions:
              contents: read

            jobs:
              update-sdk:
                uses: martincostello/update-dotnet-sdk/.github/workflows/update-dotnet-sdk.yml@v3
                with:
                  labels: "dependencies,.NET"
                  user-email: ${{ vars.GIT_COMMIT_USER_EMAIL }}
                  user-name: ${{ vars.GIT_COMMIT_USER_NAME }}
                secrets:
                  application-id: ${{ secrets.UPDATER_APPLICATION_ID }}
                  application-private-key: ${{ secrets.UPDATER_APPLICATION_PRIVATE_KEY }}
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("${{ matrix.dotnet-version }}")]
    [InlineData("foo")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("a")]
    [InlineData("a.b")]
    [InlineData("a.b.c")]
    [InlineData("6.a")]
    [InlineData("6.0.a")]
    public async Task UpgradeAsync_Handles_Invalid_Versions(string version)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        var content =
            $"""
             name: build
             on: [push]
             jobs:
               build:
                 runs-on: ubuntu-latest
                 steps:",
                   - uses: actions/checkout@v4
                   - uses: actions/setup-dotnet@v4
                     with:
                       dotnet-version: {version}
                   - run: dotnet build
            """;

        await fixture.Project.AddFileAsync(".github/workflows/build.yml", content);

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
            "name: build",
            "on: [push]",
            "jobs:",
            "  build:",
            "    runs-on: ubuntu-latest",
            "    steps:",
            "      - uses: actions/checkout@v4",
            "      - uses: actions/setup-dotnet@v4",
            "        with:",
            "          dotnet-version: 6.0.x",
            "      - run: dotnet build",
        ];

        string[] expectedLines =
        [
            "name: build",
            "on: [push]",
            "jobs:",
            "  build:",
            "    runs-on: ubuntu-latest",
            "    steps:",
            "      - uses: actions/checkout@v4",
            "      - uses: actions/setup-dotnet@v4",
            "        with:",
            "          dotnet-version: 10.0.x",
            "      - run: dotnet build",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string workflow = await fixture.Project.AddFileAsync(".github/workflows/build.yaml", fileContents, encoding);

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

        string actualContent = await File.ReadAllTextAsync(workflow);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(workflow);

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

    private static GitHubActionsUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<GitHubActionsUpgrader>());
    }
}
