﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using MartinCostello.DotNetBumper.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class DockerfileUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    ContainerRegistryClient registryClient,
    BumperLogContext logContext,
    IOptions<UpgradeOptions> options,
    ILogger<DockerfileUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading Dockerfiles";

    protected override string InitialStatus => "Update Dockerfiles";

    protected override IReadOnlyList<string> Patterns { get; } = ["*Dockerfile"];

    internal static Match DockerImageMatch(string value) => DockerImage().Match(value);

    internal static bool? IsDotNetImage(string image)
    {
        string[] parts = image.Split('/');

        if (parts.Length is 2 && !image.Contains('.', StringComparison.Ordinal))
        {
            // Image hosted in Dockerhub - unlikely to be anything official to do with .NET
            return false;
        }
        else if (parts.Length > 1 &&
                 string.Equals(parts[0], "mcr.microsoft.com", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(parts[1], "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // Official .NET container image
            return true;
        }

        // Unknown
        return null;
    }

    internal static bool TryUpdateImage(
        string image,
        Version channel,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (IsDotNetImage(image) is false)
        {
            return false;
        }

        var builder = new StringBuilder();

        bool edited = AppendImage(builder, image, channel);

        if (edited)
        {
            updated = builder.ToString();
            Debug.Assert(!string.Equals(image, updated, StringComparison.Ordinal), "The container image was not updated.");
        }

        return edited;
    }

    internal static async Task<(bool Result, string? Updated)> TryUpdateImageAsync(
        ContainerRegistryClient client,
        string current,
        Version channel,
        DotNetSupportPhase supportPhase,
        CancellationToken cancellationToken)
    {
        // See https://docs.docker.com/engine/reference/builder/#from for the syntax
        var match = DockerImageMatch(current);

        if (match.Success)
        {
            var platform = match.Groups["platform"].Value;
            var image = match.Groups["image"].Value;
            var name = match.Groups["name"].Value;
            var currentTag = match.Groups["tag"].Value;

            if (IsDotNetImage(image) is false)
            {
                return (false, null);
            }

            var builder = new StringBuilder("FROM ");

            if (!string.IsNullOrEmpty(platform))
            {
                builder.Append(platform)
                       .Append(' ');
            }

            bool edited = AppendImage(builder, image, channel);

            if (!string.IsNullOrEmpty(currentTag))
            {
                builder.Append(':');

                if (AppendTag(currentTag, channel, supportPhase, out var updatedTag))
                {
                    builder.Append(updatedTag);
                    edited = true;

                    if (!string.IsNullOrEmpty(match.Groups["digest"].Value))
                    {
                        var digest = await client.GetImageDigestAsync(image, updatedTag, cancellationToken);

                        if (digest is not null)
                        {
                            builder.Append('@')
                                   .Append(digest);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(name))
            {
                builder.Append(' ')
                       .Append(name);
            }

            if (edited)
            {
                var updated = builder.ToString();

                Debug.Assert(!string.Equals(current, updated, StringComparison.Ordinal), "The Docker image was not updated.");

                return (true, updated);
            }
        }

        return (false, null);

        static bool AppendTag(
            ReadOnlySpan<char> tag,
            Version channel,
            DotNetSupportPhase supportPhase,
            [NotNullWhen(true)] out string? updatedTag)
        {
            var maybeVersion = tag;
            var suffix = ReadOnlySpan<char>.Empty;
            var index = maybeVersion.IndexOf('-');

            if (index is not -1)
            {
                suffix = maybeVersion[(index + 1)..];
                maybeVersion = maybeVersion[..index];
            }

            if (Version.TryParse(maybeVersion, out var version) && version < channel)
            {
                var builder = new StringBuilder()
                    .Append(channel);

                const string PreviewSuffix = "preview";

                if (!suffix.IsEmpty)
                {
                    if (supportPhase == DotNetSupportPhase.Preview)
                    {
                        if (!suffix.StartsWith(PreviewSuffix, StringComparison.Ordinal))
                        {
                            builder.Append('-');
                            builder.Append(PreviewSuffix);
                        }
                    }
                    else if (suffix.StartsWith(PreviewSuffix, StringComparison.Ordinal))
                    {
                        suffix = suffix[PreviewSuffix.Length..];
                    }

                    if (suffix.Length > 0)
                    {
                        builder.Append('-');
                        suffix = LinuxDistros.TryUpdateDistro(channel, suffix);
                    }

                    builder.Append(suffix);
                }
                else if (supportPhase == DotNetSupportPhase.Preview)
                {
                    builder.Append('-');
                    builder.Append(PreviewSuffix);
                }

                updatedTag = builder.ToString();

                return true;
            }
            else
            {
                updatedTag = null;
                return false;
            }
        }
    }

    internal static bool TryUpdatePort(
        string current,
        Version channel,
        [NotNullWhen(true)] out string? updated)
    {
        updated = null;

        if (channel < DotNetVersions.EightPointZero)
        {
            return false;
        }

        var remaining = current.AsSpan();

        const string Prefix = "EXPOSE ";

        // See https://learn.microsoft.com/dotnet/core/compatibility/containers/8.0/aspnet-port
        const string BeforeDotNet8Port = "80";
        const string DotNet8AndLaterPort = "8080";

        if (remaining.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new StringBuilder(current.Length);
            builder.Append(remaining[..Prefix.Length]);

            remaining = remaining[Prefix.Length..];

            int start = remaining.IndexOfAnyInRange('1', '9');

            if (start is not -1)
            {
                int end = remaining.IndexOfAnyExceptInRange('0', '9');

                if (end is -1)
                {
                    end = remaining.Length;
                }

                int length = end - start;

                var port = remaining[start..length];

                if (port.SequenceEqual(BeforeDotNet8Port))
                {
                    builder.Append(DotNet8AndLaterPort)
                           .Append(remaining[(start + length)..]);

                    updated = builder.ToString();
                    return current != updated;
                }
            }
        }

        return false;
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

    private static bool AppendImage(
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

    [GeneratedRegex(@"^(?i)FROM(?-i) ((?<platform>--platform=[\$\w]+)\s)?(?<image>[\w\.\/\-]+)(:(?<tag>[\w\-\.]+)(@sha256:(?<digest>[0-9a-f{64}]+))?)?(\s(?<name>(?i)AS(?-i) [\S]+))?$")]
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
        var portsUpdated = false;

        using var buffered = new MemoryStream();

        using (var input = FileHelpers.OpenRead(path, out var metadata))
        using (var reader = new StreamReader(input, metadata.Encoding))
        using (var writer = new StreamWriter(buffered, metadata.Encoding, leaveOpen: true))
        {
            writer.NewLine = metadata.NewLine;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                (var result, var updated) = await TryUpdateImageAsync(
                    registryClient,
                    line,
                    upgrade.Channel,
                    upgrade.SupportPhase,
                    cancellationToken);

                if (result && updated is not null)
                {
                    line = updated;
                    edited |= true;
                }
                else if (TryUpdatePort(line, upgrade.Channel, out updated))
                {
                    line = updated;
                    edited |= true;
                    portsUpdated |= true;
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

            await using var output = File.OpenWrite(path);

            await buffered.CopyToAsync(output, cancellationToken);
            await buffered.FlushAsync(cancellationToken);

            output.SetLength(buffered.Position);

            if (portsUpdated)
            {
                logContext.Changelog.Add("Update exposed Docker container ports");
                Console.WriteWarningLine($"The exposed port(s) in {name} were updated to match .NET {DotNetVersions.EightPointZero}+ conventions.");
                Console.WriteWarningLine("Review whether any container orchestration configuration is compatible with the changes.");
            }

            return ProcessingResult.Success;
        }

        return ProcessingResult.None;
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading Dockerfiles.")]
        public static partial void UpgradingDockerfiles(ILogger logger);
    }
}
