// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NSubstitute;

namespace MartinCostello.DotNetBumper.PostProcessors;

public class LeftoverReferencesPostProcessorTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task FindReferencesAsync_Finds_Target_Frameworks_Not_Matching_The_Upgrade()
    {
        // Arrange
        string fileContents =
            """
            # Project

            This is my project, it is great.

            It currently supports the following target frameworks:

            - net6.0
            - net8.0

            To build it, run the following command:

            ```sh
            dotnet publish --configuration Release --framework net6.0 --runtime win10-x64
            ```

            It supports AWS Lambda for the `dotnet6` runtimes and `dotnet8` runtimes.

            It also multi-targets `net6.0` and `net8.0` and is published for `win-x64`.

            To create a deployment artifact, run the following command:

            ```sh
            dotnet tool install Amazon.Lambda.Tools --tool-path tools
            & (Join-Path "./tools" "dotnet-lambda") package --project-location . --output-package package.zip
            ```
            """;

        var fixture = new UpgraderFixture(outputHelper);
        var channel = new Version(8, 0);

        string relativePath = Path.Join("README.md");
        string fullPath = await fixture.Project.AddFileAsync(relativePath, fileContents);

        var projectFile = new ProjectFile(fullPath, relativePath);

        // Act
        var actual = await LeftoverReferencesPostProcessor.FindReferencesAsync(projectFile, channel, fixture.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(3);

        actual[0].Line.ShouldBe(7);
        actual[0].Column.ShouldBe(3);
        actual[0].Text.ShouldBe("net6.0");

        actual[1].Line.ShouldBe(13);
        actual[1].Column.ShouldBe(52);
        actual[1].Text.ShouldBe("net6.0");

        actual[2].Line.ShouldBe(13);
        actual[2].Column.ShouldBe(69);
        actual[2].Text.ShouldBe("win10-x64");

        // Arrange
        relativePath = Path.Join("version.txt");
        fullPath = await fixture.Project.AddFileAsync(relativePath, "1.0.0");

        projectFile = new ProjectFile(fullPath, relativePath);

        // Act
        actual = await LeftoverReferencesPostProcessor.FindReferencesAsync(projectFile, channel, fixture.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Count.ShouldBe(0);
    }

    [Fact]
    public async Task EnumerateProjectFilesAsync_When_No_Files()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        fixture.UserConfiguration.RemainingReferencesIgnore.Add("tests\\fixtures\\*");

        await fixture.Project.AddFileAsync("tests/fixtures/testdata.txt", "net6.0");

        var target = CreateTarget(fixture);

        // Act
        var actual = await target
            .EnumerateProjectFilesAsync(fixture.CancellationToken)
            .ToListAsync(fixture.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnumerateProjectFilesAsync_When_No_Git_Directory()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddFileAsync("build.sh", "dotnet publish --framework \"net6.0\"");
        await fixture.Project.AddFileAsync("file.txt", "Hello, World!");
        await fixture.Project.AddFileAsync("src/Program.cs", "Console.WriteLine(\"Hello, World!\"");
        await fixture.Project.AddFileAsync("src/Project.csproj", "<Project/>");

        var target = CreateTarget(fixture);

        // Act
        var actual = await target
            .EnumerateProjectFilesAsync(fixture.CancellationToken)
            .ToListAsync(fixture.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldNotBeEmpty();
        actual.Count.ShouldBe(4);

        actual.ShouldContain((p) => p.RelativePath == "build.sh");
        actual.ShouldContain((p) => p.RelativePath == "file.txt");
        actual.ShouldContain((p) => p.RelativePath == "src/Program.cs");
        actual.ShouldContain((p) => p.RelativePath == "src/Project.csproj");
    }

    [Theory]
    [InlineData(false, new[] { ".idea/config", ".vs/config", "file.txt", "src/Program.cs", "src/Project.csproj" })]
    [InlineData(true, new[] { ".gitignore", "file.txt", "src/Program.cs", "src/Project.csproj" })]
    public async Task EnumerateProjectFilesAsync_With_Git_Repository(
        bool hasGitIgnore,
        string[] expectedFiles)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        fixture.Project.AddGitRepository();

        if (hasGitIgnore)
        {
            await fixture.Project.AddGitIgnoreAsync();
        }

        await fixture.Project.AddFileAsync("demo.mp4", "<A video>");
        await fixture.Project.AddFileAsync("file.txt", "Hello, World!");
        await fixture.Project.AddFileAsync("logo.png", "<An image>");

        await fixture.Project.AddFileAsync(".idea/config", "<Some Rider config>");
        await fixture.Project.AddFileAsync(".vs/config", "<Some Visual Studio config>");

        await fixture.Project.AddFileAsync("src/Program.cs", "Console.WriteLine(\"Hello, World!\"");
        await fixture.Project.AddFileAsync("src/Project.csproj", "<Project/>");

        await fixture.Project.AddFileAsync("src/bin/Project.dll", "10010101");
        await fixture.Project.AddFileAsync("src/bin/Project.pdb", "0xdeadbeef");

        var target = CreateTarget(fixture);

        // Act
        var actual = await target
            .EnumerateProjectFilesAsync(fixture.CancellationToken)
            .ToListAsync(fixture.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.ShouldNotBeEmpty();
        actual.Count.ShouldBe(expectedFiles.Length);

        foreach (string fileName in expectedFiles)
        {
            actual.ShouldContain((p) => p.RelativePath == fileName, fileName);
        }
    }

    [Fact]
    public async Task PostProcessAsync_Succeeds()
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);
        fixture.Project.AddGitRepository();

        await fixture.Project.AddFileAsync("file.txt", "net6.0");
        await fixture.Project.AddFileAsync("README.md", "Hello World!");
        await fixture.Project.AddFileAsync("src/Program.cs", "Console.WriteLine(\"Hello, World!\"");
        await fixture.Project.AddFileAsync("src/Project.csproj", "<Project/>");

        await fixture.Project.AddGitIgnoreAsync();
        await fixture.Project.AddFileAsync("src/bin/Project.dll", "10010101");
        await fixture.Project.AddFileAsync("src/bin/Project.pdb", "0xdeadbeef");

        var target = CreateTarget(fixture);

        var upgrade = new UpgradeInfo()
        {
            Channel = new(8, 0),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new("8.0.100"),
            SupportPhase = DotNetSupportPhase.Active,
        };

        // Act
        var actual = await target.PostProcessAsync(upgrade, fixture.CancellationToken);

        // Assert
        actual.ShouldBe(ProcessingResult.Success);
    }

    private static LeftoverReferencesPostProcessor CreateTarget(UpgraderFixture fixture)
    {
        var environment = Substitute.For<IEnvironment>();
        environment.SupportsLinks.Returns(true);

        var options = fixture.CreateOptions();

        var configurationLoader = Substitute.For<BumperConfigurationLoader>(
            options,
            fixture.CreateLogger<BumperConfigurationLoader>());

        configurationLoader.LoadAsync(Arg.Any<CancellationToken>())
                           .Returns(fixture.UserConfiguration);

        var configurationProvider = new BumperConfigurationProvider(
            configurationLoader,
            options,
            fixture.CreateLogger<BumperConfigurationProvider>());

        return new(
            fixture.Console,
            environment,
            configurationProvider,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<LeftoverReferencesPostProcessor>());
    }
}
