﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml;
using System.Xml.Linq;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class TargetFrameworkUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<TargetFrameworkUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading target frameworks";

    protected override string InitialStatus => "Update TFMs";

    protected override IReadOnlyList<string> Patterns => ["Directory.Build.props", "*.csproj", "*.fsproj", "*.pubxml"];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(logger);

        var result = ProcessingResult.None;
        var newTfm = upgrade.Channel.ToTargetFramework();

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

            foreach (var property in project.Root.Elements("PropertyGroup").Elements())
            {
                string current = property.Value;

                if (CanUpgradeTargetFramework(current, upgrade.Channel, out var append))
                {
                    string updated = append ? $"{current};{newTfm}" : newTfm;

                    if (!string.Equals(current, updated, StringComparison.Ordinal))
                    {
                        property.SetValue(updated);
                        edited = true;
                    }
                }
            }

            if (edited)
            {
                context.Status = StatusMessage($"Updating {name}...");

                // Ensure that the user's own formatting is preserved
                string xml = project.ToString(SaveOptions.DisableFormatting);

                var writerSettings = new XmlWriterSettings()
                {
                    Async = true,
                    Encoding = metadata?.Encoding ?? Encoding.UTF8,
                    Indent = true,
                    NewLineChars = metadata?.NewLine ?? Environment.NewLine,
                    OmitXmlDeclaration = true,
                };

                using var writer = XmlWriter.Create(filePath, writerSettings);
                await project.WriteToAsync(writer, cancellationToken);

                result = result.Max(ProcessingResult.Success);
            }
        }

        if (result is ProcessingResult.Success)
        {
            logContext.Changelog.Add($"Update target framework to `{newTfm}`");
        }

        return result;
    }

    private static bool CanUpgradeTargetFramework(ReadOnlySpan<char> property, Version channel, out bool append)
    {
        append = false;

        const char Delimiter = ';';
        var remaining = property;

        int validTfms = 0;
        int updateableTfms = 0;

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOf(Delimiter);
            var part = index is -1 ? remaining : remaining[..index];

            if (!part.IsEmpty)
            {
                if (!part.IsTargetFrameworkMoniker())
                {
                    if (!part.StartsWith("net4") &&
                        !part.StartsWith("netstandard"))
                    {
                        return false;
                    }
                }
                else
                {
                    var version = part.ToVersionFromTargetFramework();

                    if (version is null || version >= channel)
                    {
                        return false;
                    }

                    updateableTfms++;
                }

                validTfms++;
            }

            remaining = remaining[(index + 1)..];

            if (index is -1)
            {
                break;
            }
        }

        append = validTfms > 1;
        return updateableTfms > 0;
    }

    private async Task<(XDocument? Project, FileMetadata? Metadata)> LoadProjectAsync(
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
            Log.FailedToLoadProject(logger, filePath, ex);
            return default;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading target framework moniker.")]
        public static partial void UpgradingTargetFramework(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Failed to parse project file {FileName}.")]
        public static partial void FailedToLoadProject(ILogger logger, string fileName, Exception exception);
    }
}
