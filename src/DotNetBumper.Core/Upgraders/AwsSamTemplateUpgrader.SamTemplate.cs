// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed partial class AwsSamTemplateUpgrader
{
    private abstract class SamTemplate(string fileName)
    {
        protected const string FormatVersionProperty = "AWSTemplateFormatVersion";
        protected const string RuntimeProperty = "Runtime";

        public string FileName { get; } = fileName;

        /// <summary>
        /// Gets the supported AWS template format versions.
        /// </summary>
        /// <remarks>
        /// See https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/format-version-structure.html.
        /// </remarks>
        protected static string[] AWSTemplateFormatVersions { get; } = ["2010-09-09"];

        public abstract bool IsValid();

        public abstract Task<(ProcessingResult Result, bool UnsupportedRuntime)> TryUpgradeAsync(
            string? runtime,
            UpgradeInfo upgrade,
            ILogger logger,
            CancellationToken cancellationToken);
    }
}
