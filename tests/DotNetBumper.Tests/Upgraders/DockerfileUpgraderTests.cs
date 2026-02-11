// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MartinCostello.DotNetBumper.Upgraders;

public class DockerfileUpgraderTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [InlineData("", null)]
    [InlineData("docker.local/base-images/dotnet-6.0-sdk", null)]
    [InlineData("amaysim/serverless", false)]
    [InlineData("rhysd/actionlint", false)]
    [InlineData("ubuntu/dotnet-deps", false)]
    [InlineData("mcr.microsoft.com/dotnet/aspnet", true)]
    [InlineData("mcr.microsoft.com/dotnet/runtime", true)]
    public static void IsDotNetImage_Returns_Correct_Value(string image, bool? expected)
    {
        // Act
        bool? actual = DockerfileUpgrader.IsDotNetImage(image);

        // Assert
        actual.ShouldBe(expected);
    }

    public static TheoryData<string, bool, string, string, string, string, string> ParsedDockerImages()
    {
        return new TheoryData<string, bool, string, string, string, string, string>
        {
            //// Valid images
            { "FROM mcr.microsoft.com/dotnet/aspnet:7.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "7.0", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:8.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "8.0", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:11.0", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "11.0", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "11.0-preview", string.Empty, string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-8.0-sdk", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, string.Empty, string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-10.0-sdk", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, string.Empty, string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-11.0-sdk", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-11.0-sdk", string.Empty, string.Empty, string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", string.Empty, string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", string.Empty, string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "11.0-resolute-chiseled-extra", string.Empty, string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "7.0", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "8.0", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "11.0", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS build", true, string.Empty, "mcr.microsoft.com/dotnet/aspnet", "11.0-preview", "AS build", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "9.0-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "11.0-resolute-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-preview-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "9.0-preview-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-preview-jammy-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "10.0-preview-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-preview-resolute-chiseled-extra AS final", true, string.Empty, "mcr.microsoft.com/dotnet/runtime-deps", "11.0-preview-resolute-chiseled-extra", "AS final", string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-8.0-sdk AS build", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-10.0-sdk AS build", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM docker.custom-domain.com/base-images/dotnet-11.0-sdk AS build", true, string.Empty, "docker.custom-domain.com/base-images/dotnet-11.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "8.0-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:10.0-jammy-chiseled-extra AS final", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "10.0-jammy-chiseled-extra", "AS final", string.Empty },
            { "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra AS final", true, string.Empty, "docker-virtual.custom-domain.com/dotnet/runtime-deps", "11.0-resolute-chiseled-extra", "AS final", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "7.0", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "8.0", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:11.0", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "11.0", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-preview", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0-preview", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:11.0-preview", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "11.0-preview", string.Empty, string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:7.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "7.0", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "8.0", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:11.0 AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "11.0", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "9.0-preview", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "10.0-preview", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/aspnet", "11.0-preview", "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0.301@sha256:faa2daf2b72cbe787ee1882d9651fa4ef3e938ee56792b8324516f5a448f3abe AS build", true, "--platform=$BUILDPLATFORM", "mcr.microsoft.com/dotnet/sdk", "9.0.301", "AS build", "faa2daf2b72cbe787ee1882d9651fa4ef3e938ee56792b8324516f5a448f3abe" },
            { "FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-8.0-sdk AS build", true, "--platform=$BUILDPLATFORM", "docker.custom-domain.com/base-images/dotnet-8.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-10.0-sdk AS build", true, "--platform=$BUILDPLATFORM", "docker.custom-domain.com/base-images/dotnet-10.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-11.0-sdk AS build", true, "--platform=$BUILDPLATFORM", "docker.custom-domain.com/base-images/dotnet-11.0-sdk", string.Empty, "AS build", string.Empty },
            { "FROM mcr.microsoft.com/vscode/devcontainers/dotnet:latest@sha256:6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f", true, string.Empty, "mcr.microsoft.com/vscode/devcontainers/dotnet", "latest", string.Empty, "6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f" },
            //// Invalid/unsupported images
            { string.Empty, false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty },
            { "foo", false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty },
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
        string expectedName,
        string expectedDigest)
    {
        // Act
        var actual = DockerfileUpgrader.DockerImageMatch(value);

        // Assert
        actual.Success.ShouldBe(expectedSuccess);
        actual.Groups["platform"].Value.ShouldBe(expectedPlatform);
        actual.Groups["image"].Value.ShouldBe(expectedImage);
        actual.Groups["tag"].Value.ShouldBe(expectedTag);
        actual.Groups["name"].Value.ShouldBe(expectedName);
        actual.Groups["digest"].Value.ShouldBe(expectedDigest);
    }

    public static TheoryData<string, string, DotNetSupportPhase, bool, string?> DockerImages()
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
            ("11.0", DotNetSupportPhase.Active),
            ("11.0", DotNetSupportPhase.Preview),
            ("11.0", DotNetSupportPhase.GoLive),
        ];

        var testCases = new TheoryData<string, string, DotNetSupportPhase, bool, string?>()
        {
            // Invalid/unsupported images
            { string.Empty, "0.0", DotNetSupportPhase.Active, false, null },
            { " ", "0.0", DotNetSupportPhase.Active, false, null },
            { "foo", "0.0", DotNetSupportPhase.Active, false, null },
            { "FROM mcr.microsoft.com/vscode/devcontainers/dotnet:latest@sha256:6e5d9440418393a00b05d306bf45bbab97d4bb9771f2b5d52f5f2304e393cc2f", "8.0", DotNetSupportPhase.Active, false, null },
        };

        // Already up-to-date images
        foreach ((var channel, var type) in channels)
        {
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}", channel, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}-preview", channel, type, false, null);
            testCases.Add($"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk", channel, type, false, null);
            testCases.Add($"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-alpine-extra", channel, type, false, null);

            // With name
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel} AS build", channel, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel} AS build-env", channel, type, false, null);
            testCases.Add($"FROM mcr.microsoft.com/dotnet/aspnet:{channel}-preview AS build", channel, type, false, null);
            testCases.Add($"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build", channel, type, false, null);
            testCases.Add($"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-alpine-extra AS final", channel, type, false, null);

            // With platform
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}-preview", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-alpine-extra", channel, type, false, null);

            // With platform and name
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel} AS build", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel} AS build-env", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}-preview AS build", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build", channel, type, false, null);
            testCases.Add($"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}-alpine-extra AS final", channel, type, false, null);
        }

        foreach ((var channel, var type) in channels)
        {
            var suffix = type is DotNetSupportPhase.Preview ? "-preview" : string.Empty;

            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0", channel, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0-preview", channel, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-alpine-extra", channel, type, true, $"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-alpine-extra");
            testCases.Add("FROM docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("From docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("from docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");

            // With name
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build", channel, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS build-env", channel, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build-env");
            testCases.Add("FROM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build", channel, type, true, $"FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-alpine-extra AS final", channel, type, true, $"FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-alpine-extra AS final");
            testCases.Add("FROM docker.custom-domain.com/base-images/dotnet-6.0-sdk AS build", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build");
            testCases.Add("From docker.custom-domain.com/base-images/dotnet-6.0-sdk As build", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk As build");
            testCases.Add("from docker.custom-domain.com/base-images/dotnet-6.0-sdk as build", channel, type, true, $"FROM docker.custom-domain.com/base-images/dotnet-{channel}-sdk as build");

            // With platform
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0", channel, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview", channel, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-alpine-extra", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-alpine-extra");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("From --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");
            testCases.Add("from --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk");

            // With platform and name
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0 AS build", channel, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build", channel, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build");
            testCases.Add("FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:6.0-preview AS build-env", channel, type, true, $"FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix} AS build-env");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-alpine-extra AS final", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:{channel}{suffix}-alpine-extra AS final");
            testCases.Add("FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk AS build", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk AS build");
            testCases.Add("From --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk As build", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk As build");
            testCases.Add("from --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-6.0-sdk as build", channel, type, true, $"FROM --platform=$BUILDPLATFORM docker.custom-domain.com/base-images/dotnet-{channel}-sdk as build");

            // With specific labels
            testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-nanoserver-ltsc2022", channel, type, true, $"FROM mcr.microsoft.com/dotnet/sdk:{channel}{suffix}-nanoserver-ltsc2022");
            testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-nanoserver-1809", channel, type, true, $"FROM mcr.microsoft.com/dotnet/sdk:{channel}{suffix}-nanoserver-1809");
            testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-windowsservercore-ltsc2019", channel, type, true, $"FROM mcr.microsoft.com/dotnet/sdk:{channel}{suffix}-windowsservercore-ltsc2019");
            testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-windowsservercore-ltsc2022", channel, type, true, $"FROM mcr.microsoft.com/dotnet/sdk:{channel}{suffix}-windowsservercore-ltsc2022");
        }

        // Mariner/Azure Linux 3.0
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0-distroless", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0-distroless");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-cbl-mariner", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-cbl-mariner2.0", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-cbl-mariner2.0-distroless", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0-distroless");

        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-azurelinux3.0");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-azurelinux3.0");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0-distroless", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-azurelinux3.0-distroless");

        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-azurelinux3.0", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-azurelinux3.0");
        testCases.Add("FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-azurelinux3.0-distroless", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-azurelinux3.0-distroless");

        // With specific Alpine 3 labels
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS dotnet-sdk");

        // Upgrades for .NET versions with an Alpine version that is no longer supported
        foreach (int minor in Enumerable.Range(13, 7))
        {
            testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.{minor} AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.19 AS dotnet-sdk");
            testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.{minor} AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine3.20 AS dotnet-sdk");

            foreach (var supportPhase in new[] { DotNetSupportPhase.GoLive, DotNetSupportPhase.Active })
            {
                testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.{minor} AS dotnet-sdk", "9.0", supportPhase, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.20 AS dotnet-sdk");
            }
        }

        foreach (int minor in Enumerable.Range(15, 5))
        {
            testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine3.{minor} AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.19 AS dotnet-sdk");
            testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine3.{minor} AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine3.20 AS dotnet-sdk");

            foreach (var supportPhase in new[] { DotNetSupportPhase.GoLive, DotNetSupportPhase.Active })
            {
                testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine3.{minor} AS dotnet-sdk", "9.0", supportPhase, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.20 AS dotnet-sdk");
            }
        }

        foreach (int minor in Enumerable.Range(18, 3))
        {
            testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.{minor} AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-alpine3.20 AS dotnet-sdk");

            foreach (var supportPhase in new[] { DotNetSupportPhase.GoLive, DotNetSupportPhase.Active })
            {
                testCases.Add($"FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.{minor} AS dotnet-sdk", "9.0", supportPhase, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.20 AS dotnet-sdk");
            }
        }

        // Upgrades for .NET versions with an Alpine version that is still supported
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine3.20 AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.20 AS dotnet-sdk");

        // With specific Debian labels
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");

        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bookworm-slim AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");

        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");

        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-bookworm-slim AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS dotnet-sdk");

        // With specific Ubuntu labels
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS dotnet-sdk", "8.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS dotnet-sdk", "9.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS dotnet-sdk", "9.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-preview-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS dotnet-sdk", "9.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-sdk", "11.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-sdk", "11.0", DotNetSupportPhase.Preview, true, "FROM mcr.microsoft.com/dotnet/sdk:11.0-preview-resolute AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-sdk", "11.0", DotNetSupportPhase.GoLive, true, "FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS dotnet-sdk");
        testCases.Add("FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-sdk", "11.0", DotNetSupportPhase.Active, true, "FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute AS dotnet-sdk");

        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", "8.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra");
        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", "9.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra");
        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra", "9.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra");
        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-noble-chiseled-extra", "9.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra");
        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:10.0-noble-chiseled-extra", "11.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra");

        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", "8.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final");
        testCases.Add("FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", "9.0", DotNetSupportPhase.Active, true, "FROM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra AS final");
        testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", "8.0", DotNetSupportPhase.Active, true, "FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra");
        testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra", "9.0", DotNetSupportPhase.Active, true, "FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra");
        testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", "8.0", DotNetSupportPhase.Active, true, "FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:8.0-jammy-chiseled-extra AS final");
        testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", "9.0", DotNetSupportPhase.Active, true, "FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:9.0-noble-chiseled-extra AS final");
        testCases.Add("FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:6.0-jammy-chiseled-extra AS final", "11.0", DotNetSupportPhase.Active, true, "FROM --platform=$BUILDPLATFORM docker-virtual.custom-domain.com/dotnet/runtime-deps:11.0-resolute-chiseled-extra AS final");

        return testCases;
    }

    [Theory]
    [MemberData(nameof(DockerImages))]
    public static async Task TryUpdateImageAsync_Returns_Expected_Values(
        string value,
        string channel,
        DotNetSupportPhase supportPhase,
        bool expectedResult,
        string? expectedImage)
    {
        // Arrange
        var channelVersion = Version.Parse(channel);

        using var httpClient = new HttpClient();
        var registryClient = new ContainerRegistryClient(
            httpClient,
            ContainerDigestCache.Instance,
            NullLogger<ContainerRegistryClient>.Instance);

        // Act
        (var actualResult, var actualImage) = await DockerfileUpgrader.TryUpdateImageAsync(
            registryClient,
            value,
            channelVersion,
            supportPhase,
            TestContext.Current.CancellationToken);

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
            FROM mcr.microsoft.com/dotnet/sdk:6.0.100 AS build-env
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile, fixture.CancellationToken);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    [Theory]
    [ClassData(typeof(DotNetChannelTestData))]
    public async Task UpgradeAsync_Upgrades_Dockerfile_With_Digest(string channel)
    {
        // Arrange
        var supportPhase =
            await DotNetPreviewFixture.HasPreviewAsync() && await DotNetPreviewFixture.LatestChannelAsync() == channel ?
            DotNetSupportPhase.Preview :
            DotNetSupportPhase.Active;

        var digest = "sha256:4763240791cd850c6803964c38ec22d88b259ac7c127b4ad1000a4fd41a08e01";
        var suffix = supportPhase is DotNetSupportPhase.Preview ? "-preview" : string.Empty;

        string fileContents =
            $"""
             FROM mcr.microsoft.com/dotnet/sdk:6.0.100@{digest} AS build-env
             WORKDIR /App
             
             COPY . ./
             RUN dotnet restore
             RUN dotnet publish -c Release -o out
             
             FROM mcr.microsoft.com/dotnet/aspnet:6.0
             WORKDIR /App
             COPY --from=build-env /App/out .
             ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
             """;

        string expectedContentsExceptFirstLine =
            $"""
             WORKDIR /App

             COPY . ./
             RUN dotnet restore
             RUN dotnet publish -c Release -o out

             FROM mcr.microsoft.com/dotnet/aspnet:{channel}{suffix}
             WORKDIR /App
             COPY --from=build-env /App/out .
             ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        await fixture.Project.AddSolutionAsync("Container.sln");
        await fixture.Project.AddApplicationProjectAsync(["net6.0"]);
        await fixture.Project.AddTestProjectAsync(["net6.0"]);

        string dockerfile = await fixture.Project.AddFileAsync("Dockerfile", fileContents);

        var upgrade = new UpgradeInfo()
        {
            Channel = Version.Parse(channel),
            EndOfLife = DateOnly.MaxValue,
            ReleaseType = DotNetReleaseType.Lts,
            SdkVersion = new($"{channel}.100"),
            SupportPhase = supportPhase,
        };

        using var httpClient = new HttpClient();
        var target = CreateTarget(fixture, httpClient);

        // Act
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile, fixture.CancellationToken);

        var actualLines = actualContent.TrimEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var expectedLines = expectedContentsExceptFirstLine.TrimEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        actualLines.Skip(1).ShouldBe(expectedLines);

        var actualImage = DockerfileUpgrader.DockerImageMatch(actualLines[0]);

        actualImage.Success.ShouldBeTrue();
        actualImage.Groups["platform"].Value.ShouldBeEmpty();
        actualImage.Groups["image"].Value.ShouldBe("mcr.microsoft.com/dotnet/sdk");
        actualImage.Groups["tag"].Value.ShouldBe(channel + suffix);

        var actualDigest = actualImage.Groups["digest"].Value;
        actualDigest.ShouldNotBeNullOrWhiteSpace();
        actualDigest.ShouldNotBe(digest);
        actualDigest.Length.ShouldBe(64);

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);

        // Arrange
        if (await IsDockerInstalledAsync(fixture.CancellationToken))
        {
            using var process = Process.Start(new ProcessStartInfo("docker", ["build", "."])
            {
                WorkingDirectory = fixture.Project.DirectoryName,
            })!;

            // Act
            await process.WaitForExitAsync(fixture.CancellationToken);

            // Assert
            process.ExitCode.ShouldBe(0);
        }
    }

    [Fact]
    public async Task UpgradeAsync_Upgrades_Dockerfile_But_Does_Not_Update_Not_DotNet_Images()
    {
        // Arrange
        string fileContents =
            """
            FROM docker.local/base-images/dotnet-6.0-sdk AS build-env

            COPY . /app
            WORKDIR /app

            RUN apt-get update && apt-get install -y dos2unix
            RUN npm install
            RUN dotnet restore Application.sln

            ENV ASPNETCORE_ENVIRONMENT="Development"
            ENV AWS_ACCESS_KEY_ID="not-a-real-secret"
            ENV AWS_SECRET_ACCESS_KEY="not-a-real-secret"
            ENV AWS_SESSION_TOKEN="not-a-real-secret"

            RUN dos2unix ./localstack/deploy_app.sh
            RUN dos2unix ./build.sh
            RUN chmod +x ./build.sh
            RUN ./build.sh --skip-tests

            FROM amaysim/serverless:3.30.1 AS serverless-build
            WORKDIR /app

            ENV AWS_ACCESS_KEY_ID="not-a-real-secret"
            ENV AWS_SECRET_ACCESS_KEY="not-a-real-secret"

            COPY --from=build-env /app /app
            """;

        string expectedContents =
            """
            FROM docker.local/base-images/dotnet-8.0-sdk AS build-env

            COPY . /app
            WORKDIR /app

            RUN apt-get update && apt-get install -y dos2unix
            RUN npm install
            RUN dotnet restore Application.sln

            ENV ASPNETCORE_ENVIRONMENT="Development"
            ENV AWS_ACCESS_KEY_ID="not-a-real-secret"
            ENV AWS_SECRET_ACCESS_KEY="not-a-real-secret"
            ENV AWS_SESSION_TOKEN="not-a-real-secret"

            RUN dos2unix ./localstack/deploy_app.sh
            RUN dos2unix ./build.sh
            RUN chmod +x ./build.sh
            RUN ./build.sh --skip-tests

            FROM amaysim/serverless:3.30.1 AS serverless-build
            WORKDIR /app

            ENV AWS_ACCESS_KEY_ID="not-a-real-secret"
            ENV AWS_SECRET_ACCESS_KEY="not-a-real-secret"

            COPY --from=build-env /app /app
            """;

        using var fixture = new UpgraderFixture(outputHelper);

        string dockerfile = await fixture.Project.AddFileAsync("Dockerfile", fileContents);

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
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile, fixture.CancellationToken);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

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

        await fixture.Project.AddFileAsync("Dockerfile", content);

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
            "FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env",
            "WORKDIR /App",
            string.Empty,
            "COPY . ./",
            "RUN dotnet restore",
            "RUN dotnet publish -c Release -o out",
            string.Empty,
            "FROM mcr.microsoft.com/dotnet/aspnet:6.0",
            "WORKDIR /App",
            "expose 123 # We don't use port 80",
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
            "expose 123 # We don't use port 80",
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
        ProcessingResult actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.Success);

        string actualContent = await File.ReadAllTextAsync(dockerfile, fixture.CancellationToken);
        actualContent.ShouldBe(expectedContent);

        byte[] actualBytes = await File.ReadAllBytesAsync(dockerfile, fixture.CancellationToken);

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
    [InlineData("7.0", "EXPOSE", false, "8", "8", ProcessingResult.None)]
    [InlineData("7.0", "EXPOSE", false, "80", "80", ProcessingResult.None)]
    [InlineData("7.0", "EXPOSE", false, "123", "123", ProcessingResult.None)]
    [InlineData("7.0", "EXPOSE", false, "8080", "8080", ProcessingResult.None)]
    [InlineData("8.0", "EXPOSE", false, "8", "8", ProcessingResult.None)]
    [InlineData("8.0", "EXPOSE", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("8.0", "EXPOSE", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("8.0", "EXPOSE", false, "123", "123", ProcessingResult.None)]
    [InlineData("8.0", "EXPOSE", false, "8080", "8080", ProcessingResult.None)]
    [InlineData("9.0", "EXPOSE", false, "8", "8", ProcessingResult.None)]
    [InlineData("9.0", "EXPOSE", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("9.0", "EXPOSE", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("9.0", "EXPOSE", false, "123", "123", ProcessingResult.None)]
    [InlineData("9.0", "EXPOSE", false, "8080", "8080", ProcessingResult.None)]
    [InlineData("10.0", "EXPOSE", false, "8", "8", ProcessingResult.None)]
    [InlineData("10.0", "EXPOSE", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("10.0", "EXPOSE", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("10.0", "EXPOSE", false, "123", "123", ProcessingResult.None)]
    [InlineData("10.0", "EXPOSE", false, "8080", "8080", ProcessingResult.None)]
    [InlineData("10.0", "expose", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("10.0", "expose", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("10.0", "Expose", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("10.0", "Expose", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "EXPOSE", false, "8", "8", ProcessingResult.None)]
    [InlineData("11.0", "EXPOSE", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "EXPOSE", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "EXPOSE", false, "123", "123", ProcessingResult.None)]
    [InlineData("11.0", "EXPOSE", false, "8080", "8080", ProcessingResult.None)]
    [InlineData("11.0", "expose", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "expose", true, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "Expose", false, "80", "8080", ProcessingResult.Success)]
    [InlineData("11.0", "Expose", true, "80", "8080", ProcessingResult.Success)]
    public async Task UpgradeAsync_Upgrades_Dockerfile_Port(
        string channel,
        string expose,
        bool hasComment,
        string currentPort,
        string expectedPort,
        ProcessingResult expectedResult)
    {
        // Arrange
        string fileContents =
            $"""
             FROM mcr.microsoft.com/dotnet/sdk:{channel} AS build-env
             WORKDIR /App

             COPY . ./
             RUN dotnet restore # This is a comment with {currentPort} in it
             RUN dotnet publish -c Release -o out

             FROM mcr.microsoft.com/dotnet/aspnet:{channel}
             WORKDIR /App
             {expose} {currentPort}{(hasComment ? " # A comment" : string.Empty)}
             COPY --from=build-env /App/out .
             ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
             """;

        string expectedContents =
            $"""
             FROM mcr.microsoft.com/dotnet/sdk:{channel} AS build-env
             WORKDIR /App

             COPY . ./
             RUN dotnet restore # This is a comment with {currentPort} in it
             RUN dotnet publish -c Release -o out

             FROM mcr.microsoft.com/dotnet/aspnet:{channel}
             WORKDIR /App
             {expose} {expectedPort}{(hasComment ? " # A comment" : string.Empty)}
             COPY --from=build-env /App/out .
             ENTRYPOINT ["dotnet", "DotNet.Docker.dll"]
             """;

        using var fixture = new UpgraderFixture(outputHelper);

        string dockerfile = await fixture.Project.AddFileAsync("Custom.Dockerfile", fileContents);

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
        actualUpdated.ShouldBe(expectedResult);

        fixture.LogContext.Changelog.Add("Update exposed Docker container ports");

        string actualContent = await File.ReadAllTextAsync(dockerfile, fixture.CancellationToken);
        actualContent.TrimEnd().ShouldBe(expectedContents.TrimEnd());

        // Act
        actualUpdated = await target.UpgradeAsync(upgrade, fixture.CancellationToken);

        // Assert
        actualUpdated.ShouldBe(ProcessingResult.None);
    }

    private static DockerfileUpgrader CreateTarget(UpgraderFixture fixture, HttpClient? httpClient = null)
    {
        var registryClient = new ContainerRegistryClient(
            httpClient!,
            ContainerDigestCache.Instance,
            fixture.CreateLogger<ContainerRegistryClient>());

        return new(
            fixture.Console,
            fixture.Environment,
            registryClient,
            fixture.LogContext,
            fixture.CreateOptions(),
            fixture.CreateLogger<DockerfileUpgrader>());
    }

    private static async Task<bool> IsDockerInstalledAsync(CancellationToken cancellationToken)
    {
        using var process = new Process()
        {
            StartInfo = new("docker", ["version", "--format", "'{{.Server.Os}}'"])
            {
                RedirectStandardOutput = true,
            },
        };

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => output.Append(e.Data);

        try
        {
            process.Start();
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode is 0 && output.ToString().Contains("linux", StringComparison.OrdinalIgnoreCase);
        }
        catch (Win32Exception)
        {
            // Docker is not installed or is not available on the PATH
            return false;
        }
    }
}
