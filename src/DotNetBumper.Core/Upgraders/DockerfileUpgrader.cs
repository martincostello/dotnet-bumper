﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class DockerfileUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger<DockerfileUpgrader> logger) : FileUpgrader(console, options, logger)
{
    protected override string Action => "Upgrading Dockerfiles";

    protected override string InitialStatus => "Update Dockerfiles";

    protected override IReadOnlyList<string> Patterns => ["Dockerfile"];

    internal static Match DockerImageMatch(string value) => DockerImage().Match(value);

    internal static bool TryUpdateImage(
        string current,
        Version channel,
        DotNetSupportPhase supportPhase,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        // See https://docs.docker.com/engine/reference/builder/#from for the syntax
        var match = DockerImageMatch(current);

        if (match.Success)
        {
            var platform = match.Groups["platform"].Value;
            var image = match.Groups["image"].Value;
            var tag = match.Groups["tag"].Value;
            var name = match.Groups["name"].Value;

            var builder = new StringBuilder("FROM ");

            if (!string.IsNullOrEmpty(platform))
            {
                builder.Append(platform)
                       .Append(' ');
            }

            bool edited = AppendImage(builder, image, channel);

            if (!string.IsNullOrEmpty(tag))
            {
                builder.Append(':');

                if (AppendTag(builder, tag, channel, supportPhase))
                {
                    edited = true;
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                builder.Append(' ')
                       .Append(name);
            }

            if (edited)
            {
                updated = builder.ToString();

                Debug.Assert(!string.Equals(current, updated, StringComparison.Ordinal), "The Docker image was not updated.");

                return true;
            }
        }

        return false;

        static bool AppendImage(
            StringBuilder builder,
            string image,
            Version channel)
        {
            var matches = VersionNumbers().Matches(image);

            bool edited = false;

            if (matches.Count > 0)
            {
                var newVersion = channel.ToString();
                var remaining = image;

                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];

                    if (match.Index > 0)
                    {
                        builder.Append(remaining[..match.Index]);
                    }

                    var maybeVersion = match.ValueSpan;

                    if (Version.TryParse(maybeVersion, out var version) && version < channel)
                    {
                        builder.Append(newVersion);
                        edited = true;
                    }
                    else
                    {
                        builder.Append(maybeVersion);
                    }

                    remaining = remaining[(match.Index + match.Length)..];
                }

                builder.Append(remaining);
            }
            else
            {
                builder.Append(image);
            }

            return edited;
        }

        static bool AppendTag(
            StringBuilder builder,
            ReadOnlySpan<char> tag,
            Version channel,
            DotNetSupportPhase supportPhase)
        {
            var maybeVersion = tag;
            var suffix = ReadOnlySpan<char>.Empty;
            var index = maybeVersion.IndexOf('-');

            if (index is not -1)
            {
                suffix = maybeVersion[index..];
                maybeVersion = maybeVersion[..index];
            }

            if (Version.TryParse(maybeVersion, out var version) && version < channel)
            {
                builder.Append(channel);

                const string PreviewSuffix = "-preview";

                if (!suffix.IsEmpty)
                {
                    if (supportPhase == DotNetSupportPhase.Preview)
                    {
                        if (!suffix.StartsWith(PreviewSuffix, StringComparison.Ordinal))
                        {
                            builder.Append(PreviewSuffix);
                        }
                    }
                    else if (suffix.StartsWith(PreviewSuffix, StringComparison.Ordinal))
                    {
                        suffix = suffix[PreviewSuffix.Length..];
                    }

                    builder.Append(suffix);
                }
                else if (supportPhase == DotNetSupportPhase.Preview)
                {
                    builder.Append(PreviewSuffix);
                }

                return true;
            }
            else
            {
                builder.Append(tag);
                return false;
            }
        }
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingDockerfiles(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            result = await TryEditDockerfile(path, upgrade, context, cancellationToken);
        }

        return result;
    }

    [GeneratedRegex(@"^(?i)FROM(?-i) ((?<platform>--platform=[\$\w]+)\s)?(?<image>[\w\.\/\-]+)(:(?<tag>[\w\-\.]+))?(\s(?<name>(?i)AS(?-i) [\S]+))?$")]
    private static partial Regex DockerImage();

    [GeneratedRegex(@"[1-9]+[0-9]*\.[0-9]")]
    private static partial Regex VersionNumbers();

    private async Task<ProcessingResult> TryEditDockerfile(
        string path,
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var name = RelativeName(path);

        context.Status = StatusMessage($"Parsing {name}...");

        var edited = false;

        using var buffered = new MemoryStream();

        using (var input = FileHelpers.OpenRead(path, out var metadata))
        using (var reader = new StreamReader(input, metadata.Encoding))
        using (var writer = new StreamWriter(buffered, metadata.Encoding, leaveOpen: true))
        {
            writer.NewLine = metadata.NewLine;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (TryUpdateImage(line, upgrade.Channel, upgrade.SupportPhase, out var updated))
                {
                    line = updated;
                    edited |= true;
                }

                await writer.WriteAsync(line);
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }

        if (edited)
        {
            context.Status = StatusMessage($"Updating {name}...");

            buffered.Seek(0, SeekOrigin.Begin);

            using var output = File.OpenWrite(path);

            await buffered.CopyToAsync(output, cancellationToken);
            await buffered.FlushAsync(cancellationToken);

            buffered.SetLength(buffered.Position);

            return ProcessingResult.Success;
        }

        return ProcessingResult.None;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading Dockerfiles.")]
        public static partial void UpgradingDockerfiles(ILogger logger);
    }
}
