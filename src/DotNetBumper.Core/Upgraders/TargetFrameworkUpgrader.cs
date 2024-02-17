// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class TargetFrameworkUpgrader(
    IOptions<UpgradeOptions> options,
    ILogger<TargetFrameworkUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(logger);

        string projectPath = options.Value.ProjectPath;

        bool filesChanged = false;
        XmlWriterSettings? writerSettings = null;

        string[] files =
        [
            ..Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories),
            ..Directory.GetFiles(projectPath, "*.fsproj", SearchOption.AllDirectories),
        ];

        foreach (var filePath in files)
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
