// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsSamTemplateUpgrader
{
    private sealed class JsonSamTemplate(string fileName, JsonObject template)
        : SamTemplate(fileName)
    {
        public JsonObject Template { get; } = template;

        public override bool IsValid()
          => Template.TryGetStringProperty(AWSTemplateFormatVersion, out _, out var version) &&
             AWSTemplateFormatVersions.Contains(version, StringComparer.Ordinal);

        public override Task<(ProcessingResult Result, bool UnsupportedRuntime)> TryUpgradeAsync(
            string? runtime,
            UpgradeInfo upgrade,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
