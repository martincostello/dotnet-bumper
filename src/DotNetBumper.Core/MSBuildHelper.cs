﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal static class MSBuildHelper
{
    //// Adapted from https://github.com/microsoft/MSBuildLocator/blob/66d20e511bc5d4a022584218caeded9936ab534f/src/MSBuildLocator/MSBuildLocator.cs

    public static IDictionary<string, string?> GetSdkProperties(string sdkVersion)
    {
        var environment = new Dictionary<string, string?>();

        TryAddSdkProperties(environment, sdkVersion);

        return environment;
    }

    public static void TryAddSdkProperties(IDictionary<string, string?> environment, string sdkVersion)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable(WellKnownEnvironmentVariables.DotNetRoot);

        if (string.IsNullOrEmpty(dotnetRoot))
        {
            dotnetRoot = Path.Combine(
                Environment.GetFolderPath(
                    OperatingSystem.IsWindows() ?
                    Environment.SpecialFolder.ProgramFiles :
                    Environment.SpecialFolder.CommonApplicationData),
                "dotnet");
        }

        var dotNetSdkPath = Path.Combine(dotnetRoot, "sdk", sdkVersion);

        if (!Directory.Exists(dotNetSdkPath))
        {
            return;
        }

        environment[WellKnownEnvironmentVariables.MSBuildExePath] = Path.Combine(dotNetSdkPath, "MSBuild.dll");
        environment[WellKnownEnvironmentVariables.MSBuildExtensionsPath] = dotNetSdkPath;
        environment[WellKnownEnvironmentVariables.MSBuildSdksPath] = Path.Combine(dotNetSdkPath, "Sdks");
    }
}
