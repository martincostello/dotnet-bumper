﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class GitHubActionsUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger<GitHubActionsUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    protected override string Action => "Upgrading GitHub Actions workflows";

    protected override string InitialStatus => "Update GitHub Actions";

    protected override IReadOnlyList<string> Patterns { get; } = ["*.yaml", "*.yml"];

    protected override IReadOnlyList<string> FindFiles()
    {
        List<string> fileNames = [];

        foreach (string fileName in base.FindFiles())
        {
            var directory = PathHelpers.Normalize(Path.GetDirectoryName(fileName) ?? string.Empty);

            if (directory.EndsWith(WellKnownFileNames.GitHubActionsWorkflowsDirectory, StringComparison.OrdinalIgnoreCase))
            {
                fileNames.Add(fileName);
            }
        }

        return fileNames;
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        Log.UpgradingGitHubActionsWorkflows(logger);

        var result = ProcessingResult.None;

        foreach (var path in fileNames)
        {
            var editResult = await TryUpgradeAsync(path, upgrade, context, cancellationToken);
            result = result.Max(editResult);
        }

        return result;
    }

    private bool TryParseActionsWorkflow(
        string fileName,
        [NotNullWhen(true)] out YamlStream? workflow)
    {
        try
        {
            workflow = YamlHelpers.ParseFile(fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log.ParseActionsWorkflowFailed(logger, fileName, ex);
            workflow = null;
            return false;
        }
    }

    private async Task<ProcessingResult> TryUpgradeAsync(
        string path,
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cancellationToken);

        var name = RelativeName(path);

        context.Status = StatusMessage($"Parsing {name}...");

        if (!TryParseActionsWorkflow(path, out var workflow))
        {
            return ProcessingResult.None;
        }

        var finder = new SetupDotNetActionFinder(upgrade);
        workflow.Accept(finder);

        if (finder.LineIndexes.Count is 0)
        {
            return ProcessingResult.None;
        }

        context.Status = StatusMessage($"Updating {name}...");

        await UpdateSdkVersionsAsync(path, finder, cancellationToken);

        return ProcessingResult.Success;
    }

    private async Task UpdateSdkVersionsAsync(
        string path,
        SetupDotNetActionFinder finder,
        CancellationToken cancellationToken)
    {
        using var buffered = new MemoryStream();

        using (var input = FileHelpers.OpenRead(path, out var metadata))
        using (var reader = new StreamReader(input, metadata.Encoding))
        using (var writer = new StreamWriter(buffered, metadata.Encoding, leaveOpen: true))
        {
            writer.NewLine = metadata.NewLine;

            int i = 0;

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (finder.LineIndexes.TryGetValue(i++, out var value))
                {
                    var updated = new StringBuilder(line.Length);

                    int index = line.IndexOf(':', StringComparison.Ordinal);

                    Debug.Assert(index != -1, $"The {SetupDotNetActionFinder.VersionPropertyName} line should contain a colon.");

                    updated.Append(line[..(index + 1)]);
                    updated.Append(' ');
                    updated.Append(value);

                    // Preserve any comments
                    index = line.IndexOf('#', StringComparison.Ordinal);

                    if (index != -1)
                    {
                        updated.Append(' ');
                        updated.Append(line[index..]);
                    }

                    line = updated.ToString();
                }

                await writer.WriteAsync(line);
                await writer.WriteLineAsync();
            }

            await writer.FlushAsync(cancellationToken);
        }

        buffered.Seek(0, SeekOrigin.Begin);

        await using var output = File.OpenWrite(path);

        await buffered.CopyToAsync(output, cancellationToken);
        await buffered.FlushAsync(cancellationToken);

        buffered.SetLength(buffered.Position);

        Log.UpgradedSdkVersions(logger, path);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Upgrading GitHub Actions workflows.")]
        public static partial void UpgradingGitHubActionsWorkflows(ILogger logger);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Warning,
            Message = "Unable to parse GitHub Actions workflow file {FileName}.")]
        public static partial void ParseActionsWorkflowFailed(
            ILogger logger,
            string fileName,
            Exception exception);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Upgraded .NET SDK version for actions/setup-dotnet in {FileName}.")]
        public static partial void UpgradedSdkVersions(
            ILogger logger,
            string fileName);
    }

    private sealed class SetupDotNetActionFinder(UpgradeInfo upgrade) : YamlVisitorBase
    {
        public const string VersionPropertyName = "dotnet-version";

        public Dictionary<int, string> LineIndexes { get; } = [];

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode { Value: "steps" } &&
                value is YamlSequenceNode { Children.Count: > 0 } items)
            {
                foreach (var item in items)
                {
                    if (GetSetupDotNetStep(item) is not { } step)
                    {
                        continue;
                    }

                    var with = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "with" }).Value;

                    if (with is not YamlMappingNode { Children.Count: > 0 } inputs)
                    {
                        continue;
                    }

                    var dotnetVersion = inputs.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: VersionPropertyName }).Value;

                    if (dotnetVersion is not YamlScalarNode { Value.Length: > 0 } version)
                    {
                        continue;
                    }

                    TryUpdateVersion(version);
                }
            }

            base.VisitPair(key, value);

            static YamlMappingNode? GetSetupDotNetStep(YamlNode item)
            {
                if (item is not YamlMappingNode { Children.Count: > 0 } step)
                {
                    return null;
                }

                var uses = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "uses" }).Value;

                if (uses is not YamlScalarNode { Value.Length: > 0 } action)
                {
                    return null;
                }

                if (action.Value?.StartsWith("actions/setup-dotnet@", StringComparison.Ordinal) is false)
                {
                    return null;
                }

                return step;
            }
        }

        private void TryUpdateVersion(YamlScalarNode dotnetVersion)
        {
            string value = dotnetVersion.Value!;
            string[] versionParts = value.Split('.');

            // See https://github.com/actions/setup-dotnet?tab=readme-ov-file#supported-version-syntax
            if (versionParts.Length is not (1 or 2 or 3))
            {
                return;
            }

            bool hasFloatingVersion =
                versionParts.Length > 1 &&
                versionParts[^1].EndsWith('x');

            int featureVersion = 0;

            if (hasFloatingVersion)
            {
                featureVersion = FeatureBandFloor(upgrade.SdkVersion.Patch);
            }

            Version? current;
            Version target;

            if (versionParts.Length is 1)
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                {
                    return;
                }

                current = new Version(major, 0);
                target = upgrade.Channel;
            }
            else if (versionParts.Length is 2)
            {
                if (hasFloatingVersion)
                {
                    if (!int.TryParse(versionParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                    {
                        return;
                    }

                    current = new Version(major, 0);
                }
                else if (!Version.TryParse(value, out current))
                {
                    return;
                }

                target = upgrade.Channel;
            }
            else
            {
                Debug.Assert(versionParts.Length is 3, $"Expected 3 version parts but got {versionParts.Length}.");

                if (hasFloatingVersion)
                {
                    if (!Version.TryParse(string.Join('.', versionParts[0..2]), out var majorMinor))
                    {
                        return;
                    }

                    int band = versionParts[^1] == "x" ? 100 : (versionParts[^1][0] - '0') * 100;
                    current = new Version(majorMinor.Major, majorMinor.Minor, band);
                    target = new Version(upgrade.Channel.Major, upgrade.Channel.Minor, featureVersion);
                }
                else
                {
                    if (!Version.TryParse(value, out current))
                    {
                        return;
                    }

                    target = upgrade.SdkVersion.Version;
                }
            }

            if (current >= target)
            {
                return;
            }

            string updated;

            if (hasFloatingVersion)
            {
                updated = $"{target.ToString(versionParts.Length - 1)}.";

                if (versionParts[^1] is "x")
                {
                    updated += "x";
                }
                else
                {
                    updated += $"{featureVersion / 100}xx";
                }
            }
            else
            {
                updated = target.ToString(versionParts.Length);
            }

            if (updated != value)
            {
                LineIndexes[dotnetVersion.Start.Line - 1] = updated;
            }

            static int FeatureBandFloor(int value)
            {
                // Convert 1xx to 100, 2xx to 200, etc.
                value /= 100;
                value *= 100;
                return value;
            }
        }
    }
}
