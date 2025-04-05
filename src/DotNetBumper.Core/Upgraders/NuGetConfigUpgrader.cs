// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class NuGetConfigUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<NuGetConfigUpgrader> logger) : XmlFileUpgrader(console, environment, options, logger)
{
    public override int Order => int.MinValue + 1; // Needs to run before PackageVersionUpgrader

    protected override string Action => "Updating NuGet configuration file";

    protected override string InitialStatus => "Update NuGet configuration";

#pragma warning disable CA1308
    protected override IReadOnlyList<string> Patterns { get; } =
    [
        WellKnownFileNames.NuGetConfiguration,
        WellKnownFileNames.NuGetConfiguration.ToLowerInvariant(),
    ];
#pragma warning restore CA1308

    public override async Task<ProcessingResult> UpgradeAsync(UpgradeInfo upgrade, CancellationToken cancellationToken)
    {
        if (Options.UpgradeType is not UpgradeType.Daily)
        {
            // Suppress all output and ignore unless it's a daily build
            return ProcessingResult.None;
        }

        return await base.UpgradeAsync(upgrade, cancellationToken);
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var fileNames = FindFiles();

        if (fileNames.Count == 0)
        {
            var fileName = Path.Combine(Options.ProjectPath, WellKnownFileNames.NuGetConfiguration);

            var defaultConfiguration =
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="NuGet" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="NuGet">
                      <package pattern="*" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """;

            await File.WriteAllTextAsync(fileName, defaultConfiguration, cancellationToken);

            fileNames = [fileName];
        }

        return await UpgradeCoreAsync(upgrade, fileNames, context, cancellationToken);
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingNuGetConfiguration(Logger);

        var result = ProcessingResult.None;

        foreach (var filePath in fileNames)
        {
            var name = RelativeName(filePath);

            context.Status = StatusMessage($"Parsing {name}...");

            (var project, var metadata) = await LoadProjectAsync(filePath, cancellationToken);

            if (project is null || project.Root is null)
            {
                result = result.Max(ProcessingResult.Warning);
                continue;
            }

            bool edited = false;

            // <add key="dotnet10" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet10/nuget/v3/index.json" />
            string major = upgrade.SdkVersion.Version.ToString(1);
            string key = $"dotnet{major}";
            string indexUrl = $"https://pkgs.dev.azure.com/dnceng/public/_packaging/{key}/nuget/v3/index.json";

            if (project.Root.Name == "configuration" &&
                project.Root.Elements("packageSources").FirstOrDefault() is { } packageSources)
            {
                var add = packageSources
                    .Elements("add")
                    .FirstOrDefault((p) => p.Attribute("key")?.Value == key);

                if (add is null)
                {
                    add = new XElement("add", new XAttribute("key", key), new XAttribute("value", indexUrl));
                    packageSources.Add(Spaces(2), add, NewLine(), Spaces(2));
                    edited = true;
                }
            }

            if (project.Root.Name == "configuration" &&
                project.Root.Elements("packageSourceMapping").FirstOrDefault() is { } packageSourceMapping)
            {
                var packageSource = packageSourceMapping
                    .Elements("packageSource")
                    .FirstOrDefault((p) => p.Attribute("key")?.Value == key);

                if (packageSource is null)
                {
                    packageSource = new XElement("packageSource", new XAttribute("key", key));

                    packageSource.Add(
                        NewLine(),
                        Spaces(6),
                        new XElement("package", new XAttribute("pattern", "*")),
                        NewLine(),
                        Spaces(4));

                    packageSourceMapping.Add(Spaces(2), packageSource, NewLine(), Spaces(2));
                    edited = true;
                }
            }

            if (edited)
            {
                context.Status = StatusMessage($"Updating {name}...");

                await UpdateProjectAsync(filePath, project, metadata, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }

            static string Spaces(int count) => new(' ', count);
            string NewLine() => metadata?.NewLine ?? Environment.NewLine;
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add("Update NuGet configuration");
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Upgrading NuGet configuration.")]
        public static partial void UpgradingNuGetConfiguration(ILogger logger);
    }
}
