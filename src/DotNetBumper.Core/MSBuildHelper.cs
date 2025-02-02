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

    public static void TryAddSdkPropertiesIfVersionMismatch(IDictionary<string, string?> environmentVariables, string desiredSdkVersion)
    {
        if (Environment.GetEnvironmentVariable(WellKnownEnvironmentVariables.MSBuildSdksPath) is { Length: > 0 } sdksPath)
        {
            string? configuredSdkVersion = null;
            string[] segments = sdksPath.Split(Path.DirectorySeparatorChar);

            if (segments.Length > 1 && segments[^1] is "Sdks" && NuGet.Versioning.NuGetVersion.TryParse(segments[^2], out var version))
            {
                configuredSdkVersion = version.ToString();
            }

            if (configuredSdkVersion is not null && configuredSdkVersion != desiredSdkVersion)
            {
                TryAddSdkProperties(environmentVariables, desiredSdkVersion);
            }
        }
    }

    public static void TryAddSdkProperties(IDictionary<string, string?> environment, string sdkVersion)
    {
        var dotnetPath = DotNetProcess.TryFindDotNetInstallation();
        var dotNetSdkPath = Path.Combine(dotnetPath, "sdk", sdkVersion);

        if (!Directory.Exists(dotNetSdkPath))
        {
            return;
        }

        environment[WellKnownEnvironmentVariables.MSBuildExePath] = Path.Combine(dotNetSdkPath, "MSBuild.dll");
        environment[WellKnownEnvironmentVariables.MSBuildExtensionsPath] = dotNetSdkPath;
        environment[WellKnownEnvironmentVariables.MSBuildExtensionsPath32] = dotNetSdkPath;
        environment[WellKnownEnvironmentVariables.MSBuildExtensionsPath64] = dotNetSdkPath;
        environment[WellKnownEnvironmentVariables.MSBuildSdksPath] = Path.Combine(dotNetSdkPath, "Sdks");
    }
}
