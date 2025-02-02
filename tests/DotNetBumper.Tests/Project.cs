// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.Build.Utilities.ProjectCreation;

namespace MartinCostello.DotNetBumper;

internal sealed class Project : IDisposable
{
    private static readonly string DotNetOutdatedVersion = GetDotNetOutdatedVersion();

    private readonly TemporaryDirectory _directory = new();

    public string DirectoryName => _directory.Path;

    public static ProjectCreator Create(bool hasSdk = false)
        => ProjectCreator.Create(sdk: hasSdk ? ProjectCreatorConstants.SdkCsprojDefaultSdk : null);

    public async Task<string> AddFileAsync(string path, string content, Encoding? encoding = null)
    {
        EnsureDirectoryTree(path);

        var fullPath = GetFilePath(path);

        if (encoding is null)
        {
            await File.WriteAllTextAsync(fullPath, content);
        }
        else
        {
            await File.WriteAllTextAsync(fullPath, content, encoding);
        }

        return fullPath;
    }

    public string GetFilePath(string path) => Path.Combine(DirectoryName, path);

    public async Task<string> GetFileAsync(string path)
        => await File.ReadAllTextAsync(GetFilePath(path));

    public async Task AddGitIgnoreAsync(string? gitignore = null)
    {
        gitignore ??= new StringBuilder()
            .AppendLine(".idea")
            .AppendLine(".vs")
            .AppendLine("bin")
            .AppendLine("obj")
            .ToString();

        await AddFileAsync(".gitignore", gitignore);
    }

    public void AddGitRepository()
    {
        Directory.CreateDirectory(Path.Combine(DirectoryName, ".git"));
    }

    public async Task<string> AddGlobalJsonAsync(string sdkVersion, string path = "global.json")
    {
        var globalJson = CreateGlobalJson(sdkVersion);
        return await AddFileAsync(path, globalJson);
    }

    public async Task<string> AddApplicationProjectAsync(
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences = default,
        string path = "src/Project/Project.csproj")
    {
        return await AddProjectAsync(path, targetFrameworks, packageReferences);
    }

    public async Task<string> AddTestProjectAsync(
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences = default,
        ICollection<string>? projectReferences = default,
        string path = "tests/Project.Tests/Project.Tests.csproj",
        ICollection<KeyValuePair<string, string>>? properties = default)
    {
        packageReferences ??= [];

        string[] testPackages =
        [
            "Microsoft.NET.Test.Sdk",
            "xunit.runner.visualstudio",
            "xunit.v3",
        ];

        foreach (var id in testPackages)
        {
            packageReferences.Add(KeyValuePair.Create(id, GetNuGetPackageVersion(id)));
        }

        return await AddProjectAsync(path, targetFrameworks, packageReferences, projectReferences, properties);
    }

    public async Task<string> AddProjectAsync(
        string path,
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences = default,
        ICollection<string>? projectReferences = default,
        ICollection<KeyValuePair<string, string>>? properties = default)
    {
        var project = CreateProject(targetFrameworks, packageReferences, projectReferences, properties);
        return await AddProjectAsync(path, project);
    }

    public async Task<string> AddProjectAsync(string path, ProjectCreator project)
        => await AddFileAsync(path, project.Xml);

    public async Task<string> AddSolutionAsync(string path)
    {
        var solution = CreateSolution();
        return await AddFileAsync(path, solution);
    }

    public async Task<string> AddToolManifestAsync(string path = ".config/dotnet-tools.json")
    {
        var manifest = new JsonObject()
        {
            ["version"] = 1,
            ["isRoot"] = true,
            ["tools"] = new JsonObject()
            {
                ["dotnet-outdated-tool"] = new JsonObject()
                {
                    ["version"] = DotNetOutdatedVersion,
                    ["commands"] = new JsonArray()
                    {
                        "dotnet-outdated",
                    },
                },
            },
        };

        return await AddFileAsync(path, manifest.PrettyPrint());
    }

    public async Task<string> AddUnitTestsAsync(
        string testName = "Always_Passes_Test",
        string assertion = "Assert.True(true);",
        string path = "tests/Project.Tests/UnitTests.cs")
    {
        var testClass =
            $$"""
              using System;
              using Xunit;
              
              namespace MyProject.Tests;
              
              public static class UnitTests
              {
                  public static string[] Items { get; } = Array.Empty<string>(); // IDE0301
                  public static bool IsTrue() => string.Equals(bool.TrueString, "true"); // CA1307

                  [Fact]
                  public static void {{testName}}()
                  {
                      {{assertion}}
                  }
              }
              """;

        return await AddFileAsync(path, testClass);
    }

    public async Task<string> AddUnitTestsWithNoCodeFixesAsync(
        string testName = "Always_Passes_Test",
        string assertion = "Assert.True(true);",
        string path = "tests/Project.Tests/UnitTests.cs")
    {
        var testClass =
            $$"""
              using System;
              using Xunit;
              
              namespace MyProject.Tests;
              
              public static class UnitTests
              {
                  public static string[] Items { get; } = [];
                  public static bool IsTrue() => string.Equals(bool.TrueString, "true", StringComparison.OrdinalIgnoreCase);

                  [Fact]
                  public static void {{testName}}()
                  {
                      {{assertion}}
                  }
              }
              """;

        return await AddFileAsync(path, testClass);
    }

    public async Task<string> AddVisualStudioCodeLaunchConfigurationsAsync(
        string channel = "6.0",
        string path = ".vscode/launch.json")
    {
        var configuration =
            $$"""
              {
                "version": "0.2.0",
                "configurations": [
                  {
                    "name": "Launch app",
                    "type": "coreclr",
                    "request": "launch",
                    // Comment
                    "program": "${workspaceFolder}/src/Project/bin/Debug/net{{channel}}/Project.dll",
                    "args": [],
                    "cwd": "${workspaceFolder}/src/Project",
                    "stopAtEntry": false,
                    "internalConsoleOptions": "openOnSessionStart",
                    "serverReadyAction": {
                      "action": "openExternally",
                      "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
                    },
                    "env": {
                      "ASPNETCORE_ENVIRONMENT": "Development"
                    },
                    "sourceFileMap": {
                      "/Views": "${workspaceFolder}/src/Project/Views"
                    }
                  }
                ]
              }
              """;

        return await AddFileAsync(path, configuration);
    }

    public async Task<string> AddVisualStudioConfigurationAsync(string channel = "6.0", string path = ".vsconfig")
    {
        var configuration = new JsonObject()
        {
            ["version"] = "1.0",
            ["components"] = new JsonArray()
            {
                "Component.GitHub.VisualStudio",
                $"Microsoft.NetCore.Component.Runtime.{channel}",
                "Microsoft.NetCore.Component.SDK",
                "Microsoft.VisualStudio.Component.CoreEditor",
                "Microsoft.VisualStudio.Component.Git",
                "Microsoft.VisualStudio.Workload.CoreEditor",
            },
        };

        return await AddFileAsync(path, configuration.PrettyPrint());
    }

    public async Task<string> AddPowerShellBuildScriptAsync(Version channel, string path = "build.ps1")
    {
        var script =
            $"""
             dotnet run --framework net{channel}
             dotnet publish --runtime win10-x64 # Publish the app
             dotnet dotnet-lambda package -c release -f net{channel} # Package the Lambda
             """;

        return await AddFileAsync(path, script);
    }

    public async Task<string> AddGitHubActionsWorkflowAsync(Version channel, string path = ".github/workflows/build.yml")
    {
        var script =
            $$$"""
               name: build
               on: [push]
               
               jobs:
                 build:
               
                   runs-on: ubuntu-latest
                   strategy:
                     matrix:
                       dotnet-version: [ '{{{channel}}}.x' ]
               
                   steps:
                     - uses: actions/checkout@v4
                     - name: Setup .NET ${{ matrix.dotnet-version }}
                       uses: actions/setup-dotnet@v3
                       with:
                         dotnet-version: ${{ matrix.dotnet-version }}
               
                     - name: Publish app
                       shell: pwsh
                       run: |
                         dotnet publish --framework net{{{channel}}} --runtime win10-x64 # Publish the app
               """;

        return await AddFileAsync(path, script);
    }

    public async Task AddEditorConfigAsync(string? editorconfig = null)
    {
        editorconfig ??=
            """
            root = true

            [*]
            end_of_line = lf
            indent_size = 4
            indent_style = space
            insert_final_newline = true
            trim_trailing_whitespace = true

            [*.{cs,vb}]
            dotnet_analyzer_diagnostic.category-Style.severity = warning
            dotnet_diagnostic.IDE0022.severity = silent
            # Workaround for https://github.com/dotnet/format/issues/1623#issuecomment-1318594411
            dotnet_diagnostic.IDE0130.severity = silent

            [*.cs]
            csharp_style_namespace_declarations = file_scoped
            """;

        await AddFileAsync(".editorconfig", editorconfig);
    }

    public async Task AddDirectoryBuildPropsAsync(
        string? noWarn = null,
        bool treatWarningsAsErrors = false)
    {
        var builder = Create()
            .Property("AnalysisMode", "All")
            .Property("EnableNETAnalyzers", true)
            .Property("EnforceCodeStyleInBuild", true)
            .Property("GenerateDocumentationFile", true)
            .Property("LangVersion", "latest")
            .Property("NoWarn", $"$(NoWarn);{noWarn ?? "CA1002;CA1819;CS419;CS1570;CS1573;CS1574;CS1584;CS1591"}")
            .Property("TreatWarningsAsErrors", treatWarningsAsErrors);

        await AddFileAsync("Directory.Build.props", builder.Xml);
    }

    public void Dispose() => _directory.Dispose();

    private static string CreateGlobalJson(string sdkVersion)
    {
        var globalJson = new JsonObject()
        {
            ["sdk"] = new JsonObject()
            {
                ["version"] = sdkVersion,
            },
        };

        return globalJson.PrettyPrint();
    }

    private static ProjectCreator CreateProject(
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences,
        ICollection<string>? projectReferences,
        ICollection<KeyValuePair<string, string>>? properties = null)
    {
        var project = Create(hasSdk: true);

        if (targetFrameworks.Count is 1)
        {
            project.Property("TargetFramework", targetFrameworks[0]);
        }
        else if (targetFrameworks.Count > 1)
        {
            project.Property("TargetFrameworks", string.Join(";", targetFrameworks));
        }

        if (properties?.Count > 0)
        {
            foreach ((string name, string value) in properties)
            {
                project.Property(name, value);
            }
        }

        if (packageReferences?.Count > 0)
        {
            foreach ((string id, string version) in packageReferences)
            {
                project.ItemPackageReference(id, version);
            }
        }

        if (projectReferences?.Count > 0)
        {
            foreach (var reference in projectReferences)
            {
                project.ItemProjectReference(reference);
            }
        }

        return project;
    }

    private static string CreateSolution()
    {
#pragma warning disable SA1027
        return
            """"
                        
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{A809011B-92B2-4990-8228-599FE09BAD4F}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project", "src\Project\Project.csproj", "{4EE4B775-29EC-4F98-8F59-6C39EFE876B7}"
            EndProject
            Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "tests", "tests", "{C4907BEC-9ABD-4D12-BB95-800C76FEC1B6}"
            EndProject
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Project.Tests", "tests\Project.Tests\Project.Tests.csproj", "{B2535EC8-7FC2-4C03-A7CA-66576C096E3F}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(SolutionProperties) = preSolution
            		HideSolutionNode = FALSE
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{4EE4B775-29EC-4F98-8F59-6C39EFE876B7}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{4EE4B775-29EC-4F98-8F59-6C39EFE876B7}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{4EE4B775-29EC-4F98-8F59-6C39EFE876B7}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{4EE4B775-29EC-4F98-8F59-6C39EFE876B7}.Release|Any CPU.Build.0 = Release|Any CPU
            		{B2535EC8-7FC2-4C03-A7CA-66576C096E3F}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{B2535EC8-7FC2-4C03-A7CA-66576C096E3F}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{B2535EC8-7FC2-4C03-A7CA-66576C096E3F}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{B2535EC8-7FC2-4C03-A7CA-66576C096E3F}.Release|Any CPU.Build.0 = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(NestedProjects) = preSolution
            		{4EE4B775-29EC-4F98-8F59-6C39EFE876B7} = {A809011B-92B2-4990-8228-599FE09BAD4F}
            		{B2535EC8-7FC2-4C03-A7CA-66576C096E3F} = {C4907BEC-9ABD-4D12-BB95-800C76FEC1B6}
            	EndGlobalSection
            EndGlobal
            """";
#pragma warning restore SA1027
    }

    private static string GetDotNetOutdatedVersion()
        => GetDotNetToolVersion("dotnet-outdated-tool");

    private static string GetDotNetToolVersion(string name)
    {
        var solutionRoot = typeof(Project).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First((p) => p.Key is "SolutionRoot")
            .Value!;

        var path = Path.Combine(solutionRoot, ".config", "dotnet-tools.json");
        using var stream = File.OpenRead(path);
        using var manifest = JsonDocument.Parse(stream);

        return manifest.RootElement
            .GetProperty("tools")
            .GetProperty(name)
            .GetProperty("version")
            .GetString()!;
    }

    private static string GetNuGetPackageVersion(string name)
    {
        var solutionRoot = typeof(Project).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First((p) => p.Key is "SolutionRoot")
            .Value!;

        var path = Path.Combine(solutionRoot, "Directory.Packages.props");
        var xml = File.ReadAllText(path);

        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        var version = project
            .Root?
            .Elements(ns + "ItemGroup")
            .Elements(ns + "PackageVersion")
            .Select((p) =>
                new
                {
                    Key = p.Attribute("Include")?.Value ?? string.Empty,
                    Value = p.Attribute("Version")?.Value ?? p.Element(ns + "Version")?.Value ?? string.Empty,
                })
            .Where((p) => p.Key == name)
            .Select((p) => p.Value)
            .FirstOrDefault();

        return version ?? throw new InvalidOperationException($"Failed to get version for package {name}.");
    }

    private void EnsureDirectoryTree(string path)
    {
        string fullPath = GetFilePath(path);
        string directory = Path.GetDirectoryName(fullPath)!;

        if (Directory.Exists(directory))
        {
            return;
        }

        var stack = new Stack<string>();

        while (directory != DirectoryName)
        {
            if (!Directory.Exists(directory))
            {
                stack.Push(directory);
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        while (stack.TryPop(out string? target))
        {
            Directory.CreateDirectory(target);
        }
    }
}
