﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class TargetFrameworkUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<TargetFrameworkUpgrader> logger) : FileUpgrader(console, options, logger)
{
    protected override IReadOnlyList<string> Patterns => ["*.csproj", "*.fsproj"];

    protected override async Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(logger);

        Console.WriteLine("Upgrading target frameworks...");

        bool filesChanged = false;
        XmlWriterSettings? writerSettings = null;

        foreach (var filePath in fileNames)
        {
            (var project, var encoding) = await LoadProjectAsync(filePath, cancellationToken);

            bool edited = false;
            string newTfm = $"net{upgrade.Channel}";

            var property = project
                .Root?
                .Element("PropertyGroup")?
                .Element("TargetFramework");

            if (property is not null &&
                !string.Equals(property.Value, newTfm, StringComparison.Ordinal))
            {
                property.SetValue(newTfm);
                edited = true;
            }

            property = project
                .Root?
                .Element("PropertyGroup")?
                .Element("TargetFrameworks");

            if (property is not null &&
                !property.Value.Contains(newTfm, StringComparison.Ordinal))
            {
                property.SetValue($"{property.Value};{newTfm}");
                edited = true;
            }

            if (edited)
            {
                // Ensure that the user's own formatting is preserved
                string xml = project.ToString(SaveOptions.DisableFormatting);

                writerSettings ??= new XmlWriterSettings()
                {
                    Async = true,
                    Indent = true,
                    OmitXmlDeclaration = true,
                };

                await File.WriteAllTextAsync(
                    filePath,
                    xml,
                    encoding,
                    cancellationToken);

                filesChanged = true;
            }
        }

        return filesChanged;
    }

    private static async Task<(XDocument Project, Encoding Encoding)> LoadProjectAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        var project = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, cancellationToken);

        return (project, reader.CurrentEncoding);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Upgrading target framework moniker.")]
        public static partial void UpgradingTargetFramework(ILogger logger);
    }
}
