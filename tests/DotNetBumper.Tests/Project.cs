// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace MartinCostello.DotNetBumper;

internal sealed class Project : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("dotnet-bumper");

    public string DirectoryName => _directory.FullName;

    public Project AddDirectory(string path)
    {
        Directory.CreateDirectory(GetFilePath(path));
        return this;
    }

    public async Task<string> AddFileAsync(string path, XDocument content)
    {
        var xml = content.ToString(SaveOptions.DisableFormatting);
        return await AddFileAsync(path, xml);
    }

    public async Task<string> AddFileAsync(string path, string content)
    {
        var fullPath = GetFilePath(path);
        await File.WriteAllTextAsync(fullPath, content);

        return fullPath;
    }

    public string GetFilePath(string path) => Path.Combine(DirectoryName, path);

    public async Task<string> GetFileAsync(string path)
        => await File.ReadAllTextAsync(GetFilePath(path));

    public async Task<string> AddGlobalJsonAsync(string sdkVersion)
    {
        var globalJson = CreateGlobalJson(sdkVersion);
        return await AddFileAsync("global.json", globalJson);
    }

    public async Task<string> AddProjectAsync(
        string path,
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences = default)
    {
        var project = CreateProjectXml(targetFrameworks, packageReferences);
        return await AddFileAsync(path, project);
    }

    public async Task<string> AddSolutionAsync(string path)
    {
        var solution = CreateSolution();
        return await AddFileAsync(path, solution);
    }

    public void Dispose()
    {
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    private static string CreateGlobalJson(string sdkVersion)
    {
        return $$"""
                 {
                   "sdk": {
                     "version": "{{sdkVersion}}"
                   }
                 }
                 """;
    }

    private static XDocument CreateProjectXml(
        IList<string> targetFrameworks,
        ICollection<KeyValuePair<string, string>>? packageReferences)
    {
        string tfms = targetFrameworks.Count == 1
            ? $"<TargetFramework>{targetFrameworks[0]}</TargetFramework>"
            : $"<TargetFrameworks>{string.Join(";", targetFrameworks)}</TargetFrameworks>";

        string xml = $"""
                      <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                          {tfms}
                        </PropertyGroup>
                      </Project>
                      """
        ;

        var project = XDocument.Parse(xml);

        if (packageReferences?.Count > 0)
        {
            project.Root!.Add(
                new XElement(
                    "ItemGroup",
                    packageReferences.Select((p) =>
                        new XElement(
                            "PackageReference",
                            new XAttribute("Include", p.Key),
                            new XAttribute("Version", p.Value)))));
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
}
