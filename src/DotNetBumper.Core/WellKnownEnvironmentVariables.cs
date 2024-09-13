// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class containing the names of well-known environment variables. This class cannot be inherited.
/// </summary>
internal static class WellKnownEnvironmentVariables
{
    internal const string DirectoryBuildPropertiesPath = "DirectoryBuildPropsPath";
    internal const string DotNetNoLogo = "DOTNET_NOLOGO";
    internal const string DotNetRollForward = "DOTNET_ROLL_FORWARD";
    internal const string DotNetRoot = "DOTNET_ROOT";
    internal const string MSBuildExePath = "MSBUILD_EXE_PATH";
    internal const string MSBuildExtensionsPath = "MSBuildExtensionsPath";
    internal const string MSBuildSdksPath = "MSBuildSDKsPath";
    internal const string NoWarn = "NoWarn";
    internal const string NuGetAudit = "NuGetAudit";
    internal const string SkipResolvePackageAssets = "SkipResolvePackageAssets";
}
