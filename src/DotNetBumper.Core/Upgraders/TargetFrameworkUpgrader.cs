// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private static readonly char[] PathSeparators = ['\\', '/'];

    protected override string Action => "Upgrading target frameworks";

    protected override string InitialStatus => "Update TFMs";

    protected override IReadOnlyList<string> Patterns => ["Directory.Build.props", "*.csproj", "*.fsproj", "*.pubxml"];

    internal static bool TryUpdatePathTfm(
        string value,
        Version channel,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (!value.Split(PathSeparators).Any((p) => p.IsTargetFrameworkMoniker()))
        {
            return false;
        }

        var builder = new StringBuilder();
        var remaining = value.AsSpan();

        bool updateValue = false;

        while (!remaining.IsEmpty)
        {
            int index = remaining.IndexOfAny(PathSeparators);
            var maybeTfm = remaining;

            if (index is not -1)
            {
                maybeTfm = maybeTfm[..index];
            }

            int consumed = maybeTfm.Length;

            if (maybeTfm.IsTargetFrameworkMoniker())
            {
                if (maybeTfm.ToVersionFromTargetFramework() is { } version && version < channel)
                {
                    maybeTfm = channel.ToTargetFramework();
                    updateValue = true;
                }
            }

            builder.Append(maybeTfm);

            if (index is not -1)
            {
                builder.Append(remaining.Slice(index, 1));
                consumed++;
            }

            remaining = remaining[consumed..];
        }

        if (updateValue)
        {
            updated = builder.ToString();

            Debug.Assert(updated.Length > 0, "The updated value should have a length.");
            Debug.Assert(updated != value, "The value is was not updated.");
        }

        return updateValue;
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(logger);

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

            foreach (var property in project.Root.Elements("PropertyGroup").Elements())
            {
                if (TryUpgradeTargetFramework(property, upgrade.Channel) ||
                    TryUpgradeTargetFrameworkInPath(property, upgrade.Channel))
                {
                    edited = true;
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
            logContext.Changelog.Add($"Update target framework to `{upgrade.Channel.ToTargetFramework()}`");
        }

        return result;
    }

    private static bool TryUpgradeTargetFramework(XElement property, Version channel)
    {
        const char Delimiter = ';';

        var current = property.Value.AsSpan();
        var remaining = current;

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

        if (updateableTfms < 1)
        {
            return false;
        }

        var newTfm = channel.ToTargetFramework();
        var append = validTfms > 1;
        var updated = append ? $"{current};{newTfm}" : newTfm;

        if (!current.SequenceEqual(updated))
        {
            property.SetValue(updated);
            return true;
        }

        return false;
    }

    private static bool TryUpgradeTargetFrameworkInPath(XElement property, Version channel)
    {
        if (TryUpdatePathTfm(property.Value, channel, out var updated))
        {
            property.SetValue(updated);
            return true;
        }

        return false;
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
