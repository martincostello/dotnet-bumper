// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Buffers;
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
    private static readonly SearchValues<char> Digits = SearchValues.Create("123456789");

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
        var name = RelativeName(path);

        context.Status = StatusMessage($"Parsing {name}...");

        if (!TryParseActionsWorkflow(path, out var workflow))
        {
            return ProcessingResult.None;
        }

        var metadata = FileHelpers.GetMetadata(path);
        var contents = await File.ReadAllTextAsync(path, metadata.Encoding, cancellationToken);

        var finder = new SetupDotNetActionFinder(upgrade, metadata, contents);
        workflow.Accept(finder);

        if (finder.Edits.Count is 0)
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
        var updated = Update(finder);
        await File.WriteAllTextAsync(path, updated, finder.Metadata.Encoding, cancellationToken);

        Log.UpgradedSdkVersions(logger, path);

        static string Update(SetupDotNetActionFinder finder)
        {
            var contents = finder.Workflow.AsSpan();
            var builder = new StringBuilder(contents.Length);

            int offset = 0;

            foreach ((var location, var replacement) in finder.Edits)
            {
                // Determine how much of the original contents to copy
                int index = location.Start.Value - offset;
                int length = location.End.Value - location.Start.Value;

                // Add the existing content before the edit
                builder.Append(contents[..index]);

                // Add the replacement text
                builder.Append(replacement);

                // Consume the used content and update the offset to account
                // for possible different lengths of content being replaced.
                contents = contents[(index + length)..];
                offset += index + length;
            }

            // Add any remaning content after the last edit
            builder.Append(contents);

            return builder.ToString();
        }
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

    private sealed class SetupDotNetActionFinder(UpgradeInfo upgrade, FileMetadata metadata, string workflow) : YamlVisitorBase
    {
        public const string VersionPropertyName = "dotnet-version";

        public List<(Range Location, string Replacement)> Edits { get; } = [];

        public FileMetadata Metadata { get; } = metadata;

        public string Workflow { get; set; } = workflow;

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
            string original = Workflow[dotnetVersion.Start.Index..dotnetVersion.End.Index];
            string[] versions = original.Split(Metadata.NewLine);

            var builder = new StringBuilder();

            if (versions.Length is 1)
            {
                string version = versions[0];

                if (TryUpdateVersion(version, out var edited))
                {
                    version = edited;
                }

                builder.Append(version);
            }
            else if (versions.Length > 1)
            {
                for (int i = 0; i < versions.Length; i++)
                {
                    var version = versions[i];

                    if (!string.IsNullOrEmpty(version))
                    {
                        if (TryUpdateVersion(version, out var edited))
                        {
                            version = edited;
                        }

                        builder.Append(version);
                    }

                    if (i != versions.Length - 1)
                    {
                        builder.Append(Metadata.NewLine);
                    }
                }
            }

            string updated = builder.ToString();

            if (updated != original)
            {
                Edits.Add((new(dotnetVersion.Start.Index, dotnetVersion.End.Index), updated));
            }
        }

        private bool TryUpdateVersion(string content, [NotNullWhen(true)] out string? updated)
        {
            const int FeatureBandMultiplier = 100;
            const char FloatingVersionChar = 'x';
            const string FloatingVersionString = "x";
            const char VersionSeparator = '.';

            updated = null;

            int index = content.AsSpan().IndexOfAny(Digits);

            string prefix;
            string versionString;

            if (index is -1)
            {
                prefix = string.Empty;
                versionString = content;
            }
            else
            {
                prefix = content[..index];
                versionString = content[index..];
            }

            string[] versionParts = versionString.Split(VersionSeparator);

            // See https://github.com/actions/setup-dotnet?tab=readme-ov-file#supported-version-syntax
            if (versionParts.Length is not (1 or 2 or 3))
            {
                return false;
            }

            bool hasFloatingVersion =
                versionParts.Length > 1 &&
                versionParts[^1].EndsWith(FloatingVersionChar);

            int upgradeFeature = 0;

            if (hasFloatingVersion)
            {
                upgradeFeature = FeatureBandFloor(upgrade.SdkVersion.Patch);
            }

            Version? currentVersion;
            Version targetVersion;

            if (versionParts.Length is 1)
            {
                if (!int.TryParse(versionString, NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                {
                    return false;
                }

                // Version requires at least two parts, so convert "6" to "6.0" etc.
                currentVersion = new(major, 0);
                targetVersion = upgrade.Channel;
            }
            else if (versionParts.Length is 2)
            {
                if (hasFloatingVersion)
                {
                    if (!int.TryParse(versionParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                    {
                        return false;
                    }

                    currentVersion = new(major, 0);
                }
                else if (!Version.TryParse(versionString, out currentVersion))
                {
                    return false;
                }

                targetVersion = upgrade.Channel;
            }
            else
            {
                Debug.Assert(versionParts.Length is 3, $"Expected 3 version parts but got {versionParts.Length}.");

                if (hasFloatingVersion)
                {
                    if (!Version.TryParse(string.Join(VersionSeparator, versionParts[0..2]), out var majorMinor))
                    {
                        return false;
                    }

                    // Treat "6.0.x" as "6.0.100", "6.0.2x" as "6.0.200", etc.
                    int currentFeature =
                        versionParts[^1] == FloatingVersionString ?
                        FeatureBandMultiplier :
                        (versionParts[^1][0] - '0') * FeatureBandMultiplier;

                    currentVersion = new(majorMinor.Major, majorMinor.Minor, currentFeature);
                    targetVersion = new(upgrade.Channel.Major, upgrade.Channel.Minor, upgradeFeature);
                }
                else
                {
                    if (!Version.TryParse(versionString, out currentVersion))
                    {
                        return false;
                    }

                    // If an exact version is specified, compare to the
                    // upgrade's SDK version rather than just the channel.
                    targetVersion = upgrade.SdkVersion.Version;
                }
            }

            if (currentVersion >= targetVersion)
            {
                // Nothing to upgrade
                return false;
            }

            string upgradedVersion;

            if (hasFloatingVersion)
            {
                upgradedVersion = $"{targetVersion.ToString(versionParts.Length - 1)}.";

                if (versionParts[^1] is FloatingVersionString)
                {
                    upgradedVersion += FloatingVersionString;
                }
                else
                {
                    char featureBand = (char)('0' + (upgradeFeature / FeatureBandMultiplier));
                    upgradedVersion += string.Create(3, featureBand, static (span, first) =>
                    {
                        span[0] = first;
                        span[1] = FloatingVersionChar;
                        span[2] = FloatingVersionChar;
                    });
                }
            }
            else
            {
                // Truncate the target version to how many version parts the original version specified
                upgradedVersion = targetVersion.ToString(versionParts.Length);
            }

            upgradedVersion = prefix + upgradedVersion;

            if (upgradedVersion == versionString)
            {
                return false;
            }

            updated = upgradedVersion;
            return true;

            static int FeatureBandFloor(int value)
            {
                // Convert 1xx to 100, 2xx to 200, etc.
                value /= FeatureBandMultiplier;
                value *= FeatureBandMultiplier;
                return value;
            }
        }
    }
}
