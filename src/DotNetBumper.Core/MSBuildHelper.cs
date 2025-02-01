// Copyright (c) Martin Costello, 2024. All rights reserved.
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
            // See https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables
            string[] candidates = OperatingSystem.IsWindows() ?
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"),
            ]
            :
            [
                "/usr/local/share/dotnet",
                "/usr/share/dotnet",
                "/usr/lib/dotnet",
            ];

            string? root = null;

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    root = candidate;
                    break;
                }
            }

            dotnetRoot = root ?? candidates[0];
        }

        var dotNetSdkPath = Path.Combine(dotnetRoot, "sdk", sdkVersion);

        SetIfFileExists(WellKnownEnvironmentVariables.MSBuildExePath, Path.Combine(dotNetSdkPath, "MSBuild.dll"));
        SetIfDirectoryExists(WellKnownEnvironmentVariables.MSBuildExtensionsPath, dotNetSdkPath);
        SetIfDirectoryExists("MSBuildExtensionsPath32", dotNetSdkPath);
        SetIfDirectoryExists(WellKnownEnvironmentVariables.MSBuildSdksPath, Path.Combine(dotNetSdkPath, "Sdks"));

        void SetIfFileExists(string key, string path)
        {
            if (File.Exists(path))
            {
                environment[key] = path;
            }
        }

        void SetIfDirectoryExists(string key, string path)
        {
            if (Directory.Exists(path))
            {
                environment[key] = path;
            }
        }
    }
}
