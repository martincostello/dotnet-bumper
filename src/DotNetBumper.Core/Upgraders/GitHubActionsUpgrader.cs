// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
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
            var remaining = finder.Contents.AsSpan();
            var edited = new StringBuilder(remaining.Length);

            int offset = 0;

            foreach ((var location, var replacement) in finder.Edits)
            {
                // Determine how much of the original contents to copy
                int index = location.Start.Value - offset;
                int length = location.End.Value - location.Start.Value;

                // Add the existing content before the edit
                edited.Append(remaining[..index]);

                // Add the replacement text
                edited.Append(replacement);

                // Consume the used content and update the offset to account
                // for possible different lengths of content being replaced.
                remaining = remaining[(index + length)..];
                offset += index + length;
            }

            // Add any remaning content after the last edit
            edited.Append(remaining);

            return edited.ToString();
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

    private sealed class SetupDotNetActionFinder(UpgradeInfo upgrade, FileMetadata metadata, string contents) : YamlVisitorBase
    {
        public const string VersionPropertyName = "dotnet-version";

        private const int FeatureBandMultiplier = 100;
        private const char FloatingVersionChar = 'x';
        private const string FloatingVersionString = "x";
        private const char PrereleaseSeparator = '-';
        private const char VersionSeparator = '.';

        public List<(Range Location, string Replacement)> Edits { get; } = [];

        public string Contents { get; set; } = contents;

        public FileMetadata Metadata { get; } = metadata;

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

                    TryUpdateVersions(version);
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

        private static int FeatureBandFloor(int value)
        {
            // Convert 1xx to 100, 2xx to 200, etc.
            value /= FeatureBandMultiplier;
            value *= FeatureBandMultiplier;
            return value;
        }

        private void TryUpdateVersions(YamlScalarNode node)
        {
            string original = Contents[node.Start.Index..node.End.Index];
            List<string> values = [.. original.Split(Metadata.NewLine)];

            var content = new StringBuilder(original.Length);

            if (values.Count is 1)
            {
                // Only one version is specifed, so we just attempt to update it
                string version = values[0];

                if (TryUpdateVersion(version, out var edited))
                {
                    version = edited;
                }

                content.Append(version);
            }
            else if (values.Count > 1)
            {
                // A multi-line string was specified, which may contain multiple versions.
                // Scan through all the values to find the distinct major versions and make
                // a note of the index of the last version in the list to either insert a
                // new version after or replace if there is only one version in the value.
                HashSet<int> majorVersions = [];
                int lastIndex = -1;
                bool hasPrerelease = false;

                for (int i = 0; i < values.Count; i++)
                {
                    var version = values[i];

                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        var index = version.AsSpan().IndexOfAny(Digits);

                        if (index is -1)
                        {
                            continue;
                        }

                        var first = version[index..].Split('.')[0];

                        if (int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var major))
                        {
                            majorVersions.Add(major);
                            lastIndex = i;
                            hasPrerelease |= version.Contains(PrereleaseSeparator, StringComparison.Ordinal);
                        }
                    }
                }

                // Do we not already have a version for the upgrade version?
                if (!majorVersions.Contains(upgrade.SdkVersion.Major) || upgrade.SdkVersion.IsPrerelease || hasPrerelease)
                {
                    var index = values[lastIndex].AsSpan().IndexOfAny(Digits);
                    var prefix = values[lastIndex][..index];

                    // Get all the unique version numbers and split into their parts
                    var sdkVersions = values
                        .Where((p) => !string.IsNullOrWhiteSpace(p))
                        .Where((p) => p is not "|")
                        .Select((p) => p.Split(VersionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Select((p) => p.Length > 3 ? [p[0], p[1], string.Join(VersionSeparator, p[2..])] : p)
                        .ToArray();

                    var upgraded = GetUpgradeVersion(sdkVersions, prefix, lastIndex, upgrade.SdkVersion);

                    if (!values.Contains(upgraded))
                    {
                        if (hasPrerelease)
                        {
                            // Replace the prerelease version
                            lastIndex = values.IndexOf(values.Last((p) => p.Contains(PrereleaseSeparator, StringComparison.Ordinal)));
                            values[lastIndex] = upgraded;
                        }
                        else if (sdkVersions.Length is 1)
                        {
                            // Even though the value was a multi-line string
                            // there was only one version, so just replace it.
                            values[lastIndex] = upgraded;
                        }
                        else
                        {
                            // Add the new version at the last version in the list
                            values.Insert(lastIndex + 1, upgraded);
                        }
                    }
                }

                // Create the updated content from the edited/updated versions
                for (int i = 0; i < values.Count; i++)
                {
                    var version = values[i];

                    if (!string.IsNullOrEmpty(version))
                    {
                        content.Append(version);
                    }

                    if (i != values.Count - 1)
                    {
                        content.Append(Metadata.NewLine);
                    }
                }
            }

            string updated = content.ToString();

            if (updated != original)
            {
                Edits.Add((new(node.Start.Index, node.End.Index), updated));
            }

            static string GetUpgradeVersion(
                string[][] sdkVersionsParts,
                string prefix,
                int lastIndex,
                NuGetVersion sdkVersion)
            {
                // At a minimum we need the prefix and the major version
                var version = new StringBuilder()
                    .Append(prefix)
                    .Append(sdkVersion.Major);

                if (!sdkVersion.IsPrerelease)
                {
                    // Strip-out any pre-release versions from the SDK versions for this major version so
                    // we can consider the version format we need to use based only on the stable versions.
                    sdkVersionsParts = sdkVersionsParts
                        .Where((p) => p.Length is not 3 || !p[2].Contains(PrereleaseSeparator, StringComparison.Ordinal))
                        .ToArray();
                }

                // Only add more parts if any one of the versions has more than one
                if (!sdkVersionsParts.All((p) => p.Length is 1))
                {
                    version.Append('.');

                    if (sdkVersionsParts.All((p) => p.Length is 2))
                    {
                        if (sdkVersionsParts.All((p) => p[1] is FloatingVersionString))
                        {
                            // All versions are of the format "6.x"
                            version.Append(FloatingVersionChar);
                        }
                        else
                        {
                            // Use the minor version from the upgrade SDK version
                            version.Append(sdkVersion.Minor);
                        }
                    }
                    else
                    {
                        version.Append(sdkVersion.Minor)
                               .Append('.');

                        if (sdkVersionsParts.All((p) => p.Length is 3 && p[2] is FloatingVersionString))
                        {
                            // All versions are of the format "6.0.x"
                            version.Append(FloatingVersionChar);
                        }
                        else if (sdkVersionsParts.All((p) => p.Length is 3 && p[2].EndsWith(FloatingVersionChar)))
                        {
                            // All versions are of the format "6.0.1xx"
                            int featureBand = (FeatureBandFloor(sdkVersion.Patch) / FeatureBandMultiplier) + '0';

                            version.Append((char)featureBand)
                                   .Append(FloatingVersionChar)
                                   .Append(FloatingVersionChar);
                        }
                        else
                        {
                            // At least one version is fully-qualified (e.g.6.0.100)
                            version.Append(sdkVersion.Patch);

                            if (sdkVersion.IsPrerelease)
                            {
                                version.Append(PrereleaseSeparator)
                                       .Append(string.Join(VersionSeparator, sdkVersion.ReleaseLabels));
                            }
                        }
                    }
                }

                return version.ToString();
            }
        }

        private bool TryUpdateVersion(string content, [NotNullWhen(true)] out string? updated)
        {
            updated = null;

            // Find where the version starts in the string as it may
            // contain leading whitespace if it's a multi-line value.
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

            if (versionParts.Length > 3 && versionString.Contains(PrereleaseSeparator, StringComparison.Ordinal))
            {
                // Handle preview versions by adding the preview part to the patch version
                versionParts =
                [
                    versionParts[0],
                    versionParts[1],
                    string.Join(VersionSeparator, versionParts[2..]),
                ];
            }

            // See https://github.com/actions/setup-dotnet?tab=readme-ov-file#supported-version-syntax
            if (versionParts.Length is not (1 or 2 or 3))
            {
                return false;
            }

            bool hasFloatingVersion =
                versionParts.Length > 1 &&
                versionParts[^1].EndsWith(FloatingVersionChar);

            int targetFeature = upgrade.SdkVersion.Patch;

            NuGetVersion? currentVersion;
            NuGetVersion targetVersion;

            if (versionParts.Length is 1)
            {
                // The version should be of the format "6"
                if (!int.TryParse(versionString, NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                {
                    return false;
                }

                // Version requires at least two parts, so convert "6" to "6.0" etc.
                currentVersion = new(new Version(major, 0));
                targetVersion = new(upgrade.Channel);
            }
            else if (versionParts.Length is 2)
            {
                if (hasFloatingVersion)
                {
                    // The version should be of the format "6.x"
                    if (!int.TryParse(versionParts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major))
                    {
                        return false;
                    }

                    // Treat "6.x" as "6.0" to get the lowest possible version
                    currentVersion = new(new Version(major, 0));
                }
                else if (!NuGetVersion.TryParse(versionString, out currentVersion))
                {
                    // The version was not of the format "6.0"
                    return false;
                }

                targetVersion = new(upgrade.Channel);
            }
            else
            {
                Debug.Assert(versionParts.Length is 3, $"Expected 3 version parts but got {versionParts.Length}.");

                if (hasFloatingVersion)
                {
                    // The version should be of the format "6.0.x" or "6.0.1xx"
                    if (!Version.TryParse(string.Join(VersionSeparator, versionParts[0..2]), out var majorMinor))
                    {
                        return false;
                    }

                    string feature = versionParts[^1];

                    // Treat "6.0.x" as "6.0.100", "6.0.1xx" as "6.0.100", "6.0.2x" as "6.0.200", etc.
                    int currentFeature =
                        feature == FloatingVersionString ?
                        FeatureBandMultiplier :
                        (feature[0] - '0') * FeatureBandMultiplier;

                    currentVersion = new(majorMinor.Major, majorMinor.Minor, currentFeature);

                    targetFeature = FeatureBandFloor(upgrade.SdkVersion.Patch);
                    targetVersion = new(upgrade.SdkVersion.Major, upgrade.SdkVersion.Minor, targetFeature);
                }
                else
                {
                    // The version should be of the format "6.0.100"
                    if (!NuGetVersion.TryParse(versionString, out currentVersion))
                    {
                        return false;
                    }

                    // If an exact version is specified, compare to the
                    // upgrade's SDK version rather than just the channel.
                    targetVersion = upgrade.SdkVersion;
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
                upgradedVersion = $"{targetVersion.Version.ToString(versionParts.Length - 1)}.";

                if (versionParts[^1] is FloatingVersionString)
                {
                    // The version specified in the last part ".x" so keep it as ".x"
                    upgradedVersion += FloatingVersionString;
                }
                else
                {
                    // The version specified in the last part ".1xx" so keep it as ".Nxx"
                    char featureBand = (char)('0' + (targetFeature / FeatureBandMultiplier));
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
                upgradedVersion =
                    targetVersion.IsPrerelease && versionParts.Length is 3 ?
                    targetVersion.ToString() : // Use the exact pre-release version
                    targetVersion.Version.ToString(versionParts.Length); // Truncate the target version to how many version parts the original version specified
            }

            upgradedVersion = prefix + upgradedVersion;

            if (upgradedVersion == versionString)
            {
                // Nothing was updated
                return false;
            }

            updated = upgradedVersion;
            return true;
        }
    }
}
