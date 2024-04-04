// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
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
          => Template.TryGetStringProperty(FormatVersionProperty, out _, out var version) &&
             AWSTemplateFormatVersions.Contains(version, StringComparer.Ordinal);

        public override async Task<(ProcessingResult Result, bool UnsupportedRuntime)> TryUpgradeAsync(
            string? runtime,
            UpgradeInfo upgrade,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!JsonHelpers.UpdateStringNodes(Template, (upgrade, runtime), TryUpdateRuntime))
            {
                return (ProcessingResult.None, false);
            }
            else if (runtime is null)
            {
                return (ProcessingResult.Warning, true);
            }

            var options = new JsonWriterOptions()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            };

            await Template.SaveAsync(FileName, options, cancellationToken);

            return (ProcessingResult.Success, false);

            static bool TryUpdateRuntime(JsonValue node, (UpgradeInfo Upgrade, string? Runtime) state)
            {
                Debug.Assert(node.GetValueKind() is JsonValueKind.String, $"JSON value {node.GetPath()} is not a string. Kind: {node.GetValueKind()}");

                if (node.Parent is not JsonObject ||
                    node.GetPropertyName() is not RuntimeProperty)
                {
                    return false;
                }

                string current = node.GetValue<string>();

                if (IsSupportedRuntime(current, state.Upgrade) is null)
                {
                    // Nothing to upgrade
                    return false;
                }
                else if (state.Runtime is null)
                {
                    // We would have upgraded, but this version is not supported
                    return true;
                }

                if (current.ToVersionFromLambdaRuntime() is { } version &&
                    version >= MinimumVersion && version < state.Upgrade.Channel)
                {
                    node.ReplaceWith(JsonValue.Create(state.Runtime));
                    return true;
                }

                return false;
            }
        }
    }
}
