// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace MartinCostello.DotNetBumper;

internal static class ProjectHelpers
{
    private static readonly XNamespace None = string.Empty;
    private static readonly XNamespace MSBuild = "http://schemas.microsoft.com/developer/msbuild/2003";
    private static readonly XNamespace[] Namespaces = [None, MSBuild];

    public static List<string> FindProjects(string path, SearchOption searchOption = SearchOption.AllDirectories)
    {
        List<string> projects =
        [
            ..Directory.GetFiles(path, "*.sln", searchOption),
        ];

        if (projects.Count == 0)
        {
            projects.AddRange(Directory.GetFiles(path, "*.csproj", searchOption));
            projects.AddRange(Directory.GetFiles(path, "*.fsproj", searchOption));
        }

        return projects
            .Select(Path.GetDirectoryName)
            .Cast<string>()
            .ToList();
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
}
