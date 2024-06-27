// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;

namespace MartinCostello.DotNetBumper;

internal static class ProjectAssertionHelpers
{
    public static async Task<string?> GetSdkVersionAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var json = await fixture.Project.GetFileAsync(fileName);
        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("sdk")
            .GetProperty("version")
            .GetString();
    }

    public static async Task<Dictionary<string, string>> GetPackageReferencesAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var xml = await fixture.Project.GetFileAsync(fileName);
        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        return project
            .Root?
            .Elements(ns + "ItemGroup")
            .Elements(ns + "PackageReference")
            .Select((p) =>
                new
                {
                    Key = p.Attribute("Include")?.Value ?? string.Empty,
                    Value = p.Attribute("Version")?.Value ?? p.Element(ns + "Version")?.Value ?? string.Empty,
                })
            .ToDictionary((p) => p.Key, (p) => p.Value) ?? [];
    }

    public static async Task<string?> GetTargetFrameworksAsync(
        UpgraderFixture fixture,
        string fileName)
    {
        var xml = await fixture.Project.GetFileAsync(fileName);
        var project = XDocument.Parse(xml);

        project.Root.ShouldNotBeNull();
        var ns = project.Root.GetDefaultNamespace();

        var tfm = project
            .Root
            .Element(ns + "PropertyGroup")?
            .Element(ns + "TargetFramework")?
            .Value;

        if (tfm is not null)
        {
            return tfm;
        }

        return project
            .Root
            .Element(ns + "PropertyGroup")?
            .Element(ns + "TargetFrameworks")?
            .Value;
    }
}
