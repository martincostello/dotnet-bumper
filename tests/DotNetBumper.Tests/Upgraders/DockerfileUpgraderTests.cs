// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgraders;

public class DockerfileUpgraderTests(ITestOutputHelper outputHelper)
{
    public static TheoryData<string, bool, string, string, string, string> ParsedDockerImages()
    {
        return new TheoryData<string, bool, string, string, string, string>
        {
            //// Valid images
            { "FROM mcr.microsoft.com/dotnet/aspnet:7.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "7.0", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:8.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "8.0", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-8.0-sdk", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-10.0-sdk", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "7.0", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "8.0", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", "AS build" },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", "AS final" },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "9.0-jammy-chiseled-extra", "AS final" },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", "AS final" },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "9.0-preview-jammy-chiseled-extra", "AS final" },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-preview-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "10.0-preview-jammy-chiseled-extra", "AS final" },
            { "FROM docker.custom-domain.com/base-images/dotnet-8.0-sdk AS build", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, "AS build" },
            { "FROM docker.custom-domain.com/base-images/dotnet-10.0-sdk AS build", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, "AS build" },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", "AS final" },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra AS final", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", "AS final" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "7.0", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "8.0", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-preview", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0-preview", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "7.0", "AS build" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "8.0", "AS build" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0", "AS build" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0", "AS build" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", "AS build" },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", "AS build" },
            { "FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-8.0-sdk AS build", true, "--platform=$BUILDPLATFORM", "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, "AS build" },
            { "FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-10.0-sdk AS build", true, "--platform=$BUILDPLATFORM", "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, "AS build" },
            //// Invalid/unsupported images
            { string.Empty, false, string.Empty, string.Empty, string.Empty, string.Empty },
            { "foo", false, string.Empty, string.Empty, string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/vscode/devcontainers/dotnet:latest@sha256:6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f", false, string.Empty, string.Empty, string.Empty, string.Empty },
        };
    }

    [Theory]
    [MemberData(nameof(ParsedDockerImages))]
    public static void DockerImageMatch_Returns_Expected_Matches(
        string value,
        bool expectedSuccess,
        string expectedPlatform,
        string expectedImage,
        string expectedTag,
        string expectedName)
    {
        // Act
        var actual = DockerfileUpgrader.DockerImageMatch(value);

        // Assert
        actual.Success.ShouldBe(expectedSuccess);
        actual.Groups["platform"].Value.ShouldBe(expectedPlatform);
        actual.Groups["image"].Value.ShouldBe(expectedImage);
        actual.Groups["tag"].Value.ShouldBe(expectedTag);
        actual.Groups["name"].Value.ShouldBe(expectedName);
    }

    public static TheoryData<string, Version, DotNetSupportPhase, bool, string?> DockerImages()
    {
        (string Channel, DotNetSupportPhase Type)[] channels =
        [
            ("7.0", DotNetSupportPhase.Active),
            ("8.0", DotNetSupportPhase.Active),
            ("9.0", DotNetSupportPhase.Active),
            ("9.0", DotNetSupportPhase.Preview),
            ("9.0", DotNetSupportPhase.GoLive),
            ("10.0", DotNetSupportPhase.Active),
            ("10.0", DotNetSupportPhase.Preview),
            ("10.0", DotNetSupportPhase.GoLive),
        ];

        var testCases = new TheoryData<string, Version, DotNetSupportPhase, bool, string?>()
        {
            // Invalid/unsupported images
            { string.Empty, new(0, 0), DotNetSupportPhase.Active, false, null },
            { " ", new(0, 0), DotNetSupportPhase.Active, false, null },
            { "foo", new(0, 0), DotNetSupportPhase.Active, false, null },
            { "FROM mcr.microsoft.com/vscode/devcontainers/dotnet:latest@sha256:6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f", new(8, 0), DotNetSupportPhase.Active, false, null },
        };

        // Already up-to-date images
        foreach ((var channel, var type) in channels)
        {
            var version = Version.Parse(channel);

            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}", version, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}-preview", version, type, false, null);
            testCases.Add($"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk", version, type, false, null);
            testCases.Add($"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-jammy-chiseled-extra", version, type, false, null);

            // With name
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel} AS build", version, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel} AS build-env", version, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}-preview AS build", version, type, false, null);
            testCases.Add($"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build", version, type, false, null);
            testCases.Add($"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-jammy-chiseled-extra AS final", version, type, false, null);

            // With platform
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}-preview", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-jammy-chiseled-extra", version, type, false, null);

            // With platform and name
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel} AS build", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel} AS build-env", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}-preview AS build", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build", version, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-jammy-chiseled-extra AS final", version, type, false, null);
        }

        foreach ((var channel, var type) in channels)
        {
            var version = Version.Parse(channel);
            var suffix = type is DotNetSupportPhase.Preview ? "-preview" : string.Empty;

            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0", version, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0-preview", version, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", version, type, true, $"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-jammy-chiseled-extra");
            testCases.Add("FROM docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("From docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("from docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");

            // With name
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build", version, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build-env", version, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build-env");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build", version, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", version, type, true, $"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-jammy-chiseled-extra AS final");
            testCases.Add("FROM docker.custom-domain.com/base-images/dotnet-6.0-sdk AS build", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build");
            testCases.Add("From docker.custom-domain.com/base-images/dotnet-6.0-sdk As build", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk As build");
            testCases.Add("from docker.custom-domain.com/base-images/dotnet-6.0-sdk as build", version, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk as build");

            // With platform
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0", version, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview", version, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", version, type, true, $"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-jammy-chiseled-extra");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("From --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("from --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");

            // With platform and name
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0 AS build", version, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build", version, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build-env", version, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build-env");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", version, type, true, $"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-jammy-chiseled-extra AS final");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk AS build", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build");
            testCases.Add("From --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk As build", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk As build");
            testCases.Add("from --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk as build", version, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk as build");
        }

        return testCases;
    }

    [Theory]
    [MemberData(nameof(DockerImages))]
    public static void TryUpdateImage_Returns_Expected_Values(
        string value,
        Version channel,
        DotNetSupportPhase supportPhase,
        bool expectedResult,
        string? expectedImage)
    {
        // Act
        var actualResult = DockerfileUpgrader.TryUpdateImage(value, channel, supportPhase, out var actualImage);

        // Assert
        actualResult.ShouldBe(expectedResult);
        actualImage.ShouldBe(expectedImage);
    }

    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Dockerfile(string channel)
    {
        // Arrange
        string fileContents =
            """
            FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
            WORKDIR /App
            
            COPY . ./
            RUN dotnet restore
            RUN dotnet publish -c Release -o out
            
            FROM mcr.microsoft.com/dotnet/aspnet:6.0
            WORKDIR /App
            COPY --from=build-env /App/out .
            ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
            """;

        string expectedContents =
            $"""
             FROM mcr.microsoft.com/dotnet/sdk:{channel} AS build-env
             WORKDIR /App
             
             COPY . ./
             RUN dotnet restore
             RUN dotnet publish -c Release -o out
             
             FROM mcr.microsoft.com/dotnet/aspnet:{channel}
             WORKDIR /App
             COPY --from=build-env /App/out .
             ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string dockerfile = await fixture.Project.AddFileAsync("Dockerfile", fileContents);

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

        string actualContent = await File.ReadAllTextAsync(dockerfile);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, CancellationToken.None);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("FROM")]
    [InlineData("Not A Dockerfile")]
    [InlineData("FFROM mcr.microsoft.com/dotnet/aspnet:7.0")]
    [InlineData("FROMM mcr.microsoft.com/dotnet/aspnet:7.0")]
    [InlineData("{}")]
    public async Task UpgradeAsync_Handles_Invalid_Dockerfile(string content)
    {
        // Arrange
        using var fixture = new UpgraderFixture(outputHelper);

        string vsconfig = await fixture.Project.AddFileAsync("aws-lambda-tools-defaults.json", content);

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
            "FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env",
            "WORKDIR /App",
            string.Empty,
            "COPY . ./",
            "RUN dotnet restore",
            "RUN dotnet publish -c Release -o out",
            string.Empty,
            "FROM mcr.microsoft.com/dotnet/aspnet:6.0",
            "WORKDIR /App",
            "COPY --from=build-env /App/out .",
            "ENTRYPOINT [\"dotnet\", \"DotNet.Docker.dll\"]",
        ];

        string[] expectedLines =
        [
            "FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env",
            "WORKDIR /App",
            string.Empty,
            "COPY . ./",
            "RUN dotnet restore",
            "RUN dotnet publish -c Release -o out",
            string.Empty,
            "FROM mcr.microsoft.com/dotnet/aspnet:10.0",
            "WORKDIR /App",
            "COPY --from=build-env /App/out .",
            "ENTRYPOINT [\"dotnet\", \"DotNet.Docker.dll\"]",
        ];

        string fileContents = string.Join(newLine, originalLines) + newLine;
        string expectedContent = string.Join(newLine, expectedLines) + newLine;

        using var fixture = new UpgraderFixture(outputHelper);

        var encoding = new UTF8Encoding(hasUtf8Bom);
        string dockerfile = await fixture.Project.AddFileAsync("Dockerfile", fileContents, encoding);

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

        string actualContent = await File.ReadAllTextAsync(dockerfile);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(dockerfile);

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

    private static DockerfileUpgrader CreateTarget(UpgraderFixture fixture)
    {
        return new(
            fixture.Console,
            fixture.Environment,
            fixture.CreateOptions(),
            fixture.CreateLogger<DockerfileUpgrader>());
    }
}
