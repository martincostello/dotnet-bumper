// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

public static class PatchedGlobalJsonFileTests
{
    [Fact]
    public static async Task TryPatchAsync_Returns_Null_If_Global_Json_Does_Not_Exist()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(
            directory.Path,
            NuGetVersion.Parse("8.0.100"),
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public static async Task TryPatchAsync_Updates_Sdk_Version_When_Lower()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "6.0.100"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("8.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");
    }

    [Fact]
    public static async Task TryPatchAsync_Does_Not_Update_Sdk_Version_When_Higher()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "9.0.100"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);
        string originalContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("9.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldBe(originalContent);
    }

    [Fact]
    public static async Task TryPatchAsync_Adds_AllowPrerelease_For_Prerelease_Versions()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "8.0.100"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("9.0.100-preview.1.24101.2");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("9.0.100-preview.1.24101.2");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"9.0.100-preview.1.24101.2\"");
        updatedContent.ShouldContain("\"allowPrerelease\": true");
    }

    [Fact]
    public static async Task TryPatchAsync_Does_Not_Add_AllowPrerelease_If_Already_True()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "8.0.100",
                "allowPrerelease": true
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("9.0.100-rc.1.24452.12");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("9.0.100-rc.1.24452.12");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"9.0.100-rc.1.24452.12\"");
        updatedContent.ShouldContain("\"allowPrerelease\": true");
    }

    [Fact]
    public static async Task TryPatchAsync_Adds_Version_Property_If_Missing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("8.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");
    }

    [Fact]
    public static async Task TryPatchAsync_Preserves_Other_Sdk_Properties()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "6.0.100",
                "rollForward": "latestMinor"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("8.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");
        updatedContent.ShouldContain("\"rollForward\": \"latestMinor\"");
    }

    [Fact]
    public static async Task TryPatchAsync_Preserves_MSBuild_Sdk_Versions()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "6.0.100"
                },
                "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.7.0"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("8.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");
        updatedContent.ShouldContain("\"msbuild-sdks\"");
        updatedContent.ShouldContain("\"Microsoft.Build.NoTargets\": \"3.7.0\"");
    }

    [Fact]
    public static async Task Dispose_Restores_Original_File()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string originalContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "6.0.100"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, originalContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);
        result.ShouldNotBeNull();

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");

        result.Dispose();

        // Assert
        string restoredContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        restoredContent.ShouldBe(originalContent);
    }

    [Fact]
    public static async Task TryPatchAsync_Handles_Global_Json_Without_Sdk_Property()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "msbuild-sdks": {
                "Microsoft.Build.NoTargets": "3.7.0"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);
        string originalContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBeNull();

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldBe(originalContent);
    }

    [Fact]
    public static async Task TryPatchAsync_Handles_Sdk_As_Non_Object()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": "string value"
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);
        string originalContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBeNull();

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldBe(originalContent);
    }

    [Fact]
    public static async Task TryPatchAsync_Handles_Non_Object()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent = "[]";

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBeNull();
    }

    [Fact]
    public static async Task TryPatchAsync_Handles_Version_As_Non_String()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": 123
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(directory.Path, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBeNull();

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldBe(globalJsonContent);
    }

    [Fact]
    public static async Task TryPatchAsync_Finds_Global_Json_In_Parent_Directory()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        var cancellationToken = TestContext.Current.CancellationToken;
        string globalJsonPath = Path.Join(directory.Path, "global.json");
        string globalJsonContent =
            /*lang=json,strict*/
            """
            {
                "sdk": {
                "version": "6.0.100"
                }
            }
            """;

        await File.WriteAllTextAsync(globalJsonPath, globalJsonContent, cancellationToken);

        string subDirectory = Path.Join(directory.Path, "src");
        Directory.CreateDirectory(subDirectory);

        var sdkVersion = NuGetVersion.Parse("8.0.100");

        // Act
        var result = await PatchedGlobalJsonFile.TryPatchAsync(subDirectory, sdkVersion, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.SdkVersion.ShouldBe("8.0.100");

        string updatedContent = await File.ReadAllTextAsync(globalJsonPath, cancellationToken);
        updatedContent.ShouldContain("\"version\": \"8.0.100\"");
    }
}
