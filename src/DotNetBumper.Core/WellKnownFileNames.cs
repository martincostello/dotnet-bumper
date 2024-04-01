// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal static class WellKnownFileNames
{
    public const string CSharpProjects = "*.csproj";
    public const string FSharpProjects = "*.fsproj";
    public const string VisualBasicProjects = "*.vbproj";
    public const string PublishProfiles = "*.pubxml";
    public const string SolutionFiles = "*.sln";

    public const string AwsLambdaToolsDefaults = "aws-lambda-tools-defaults.json";
    public const string DirectoryBuildProps = "Directory.Build.props";
    public const string GlobalJson = "global.json";
    public const string ToolsManifest = "dotnet-tools.json";
}
