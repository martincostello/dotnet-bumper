// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.Build.Construction;

namespace MartinCostello.DotNetBumper;

internal static class ProjectHelpers
{
    private static readonly XNamespace None = string.Empty;
    private static readonly XNamespace MSBuild = "http://schemas.microsoft.com/developer/msbuild/2003";
    private static readonly XNamespace[] Namespaces = [None, MSBuild];

    public static List<string> FindProjects(string path, SearchOption searchOption = SearchOption.AllDirectories)
        => [.. FindProjectFiles(path, searchOption).Select(Path.GetDirectoryName).Cast<string>()];

    public static List<string> FindProjectFiles(string path, SearchOption searchOption = SearchOption.AllDirectories)
    {
        List<string> projects =
        [
            .. Directory.GetFiles(path, WellKnownFileNames.SolutionFiles, searchOption),
        ];

        if (projects.Count == 0)
        {
            projects.AddRange(Directory.GetFiles(path, WellKnownFileNames.CSharpProjects, searchOption));
            projects.AddRange(Directory.GetFiles(path, WellKnownFileNames.FSharpProjects, searchOption));
            projects.AddRange(Directory.GetFiles(path, WellKnownFileNames.VisualBasicProjects, searchOption));
        }

        return TryReduceProjects(projects);
    }

    public static List<string> GetSolutionProjects(string solutionFile)
    {
        try
        {
            var solution = SolutionFile.Parse(solutionFile);
            var projects = solution.ProjectsInOrder.Where((p) => p.ProjectType != SolutionProjectType.SolutionFolder);

            return [.. projects.Select((p) => p.AbsolutePath)];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static IEnumerable<XElement> EnumerateProperties(XElement project)
    {
        foreach (var ns in Namespaces)
        {
            foreach (var property in project.Elements(ns + "PropertyGroup").Elements())
            {
                yield return property;
            }
        }
    }

    public static string RelativeName(string projectPath, string path)
    {
        var relative = Path.GetRelativePath(projectPath, path);
        return relative is "." ? Path.GetFileName(path) : relative;
    }

    private static List<string> TryReduceProjects(List<string> fileNames)
    {
        var solutionFiles = fileNames.Where((p) => Path.GetExtension(p) is ".sln").ToList();
        var projectFiles = fileNames.Where((p) => Path.GetExtension(p) is not ".sln").ToList();

        if (solutionFiles.Count is 0 || projectFiles.Count is 0)
        {
            return fileNames;
        }

        foreach (var solutionFile in solutionFiles)
        {
            var projects = GetSolutionProjects(solutionFile);

            foreach (var projectFile in projects)
            {
                projectFiles.Remove(projectFile);
            }
        }

        return [.. solutionFiles, .. projectFiles];
    }
}
