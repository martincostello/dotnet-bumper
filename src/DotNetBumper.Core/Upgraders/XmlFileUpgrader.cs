// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal abstract partial class XmlFileUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger logger) : FileUpgrader(console, environment, options, logger)
{
    protected static async Task UpdateProjectAsync(
        string filePath,
        XDocument project,
        FileMetadata? metadata,
        CancellationToken cancellationToken)
    {
        var settings = new XmlWriterSettings()
        {
            Async = true,
            Encoding = metadata?.Encoding ?? Encoding.UTF8,
            Indent = true,
            NewLineChars = metadata?.NewLine ?? Environment.NewLine,
            OmitXmlDeclaration = project.Declaration is null,
        };

        using var writer = XmlWriter.Create(filePath, settings);
        await project.WriteToAsync(writer, cancellationToken);
    }

    protected async Task<(XDocument? Project, FileMetadata? Metadata)> LoadProjectAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        using var stream = FileHelpers.OpenRead(filePath, out var metadata);
        using var reader = new StreamReader(stream, metadata.Encoding);

        try
        {
            var project = await XDocument.LoadAsync(reader, LoadOptions.PreserveWhitespace, cancellationToken);
            return (project, metadata);
        }
        catch (Exception ex)
        {
            Log.FailedToLoadProject(Logger, filePath, ex);
            return default;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "Failed to parse project file {FileName}.")]
        public static partial void FailedToLoadProject(ILogger logger, string fileName, Exception exception);
    }
}
