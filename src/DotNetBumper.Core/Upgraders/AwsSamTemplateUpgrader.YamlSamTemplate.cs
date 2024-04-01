// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsSamTemplateUpgrader
{
    private sealed class YamlSamTemplate(string fileName, YamlStream template)
        : SamTemplate(fileName)
    {
        public YamlStream Template { get; } = template;

        public override bool IsValid()
        {
            foreach (var document in Template.Documents)
            {
                if (document.RootNode is not YamlMappingNode mapping)
                {
                    continue;
                }

                if (mapping.Children.Any(IsAwsTemplate))
                {
                    return true;
                }
            }

            return false;

            static bool IsAwsTemplate(KeyValuePair<YamlNode, YamlNode> pair) =>
                pair.Key is YamlScalarNode key && key.Value is FormatVersionProperty &&
                pair.Value is YamlScalarNode value && AWSTemplateFormatVersions.Contains(value.Value, StringComparer.Ordinal);
        }

        public override async Task<(ProcessingResult Result, bool UnsupportedRuntime)> TryUpgradeAsync(
            string? runtime,
            UpgradeInfo upgrade,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var finder = new YamlRuntimeFinder(RuntimeProperty, upgrade.Channel);
            Template.Accept(finder);

            if (finder.LineIndexes.Count is 0)
            {
                return (ProcessingResult.None, false);
            }
            else if (runtime is null)
            {
                return (ProcessingResult.Warning, true);
            }

            await UpdateRuntimesAsync(FileName, runtime, finder, logger, cancellationToken);

            return (ProcessingResult.Success, false);
        }
    }
}
