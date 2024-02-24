// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

internal sealed class GitHubActionsLogWriter(string fileName) : MarkdownLogWriter(fileName)
{
    private const string VariableName = "GITHUB_STEP_SUMMARY";

    internal GitHubActionsLogWriter()
        : this(GetSummaryFileName())
    {
    }

    private static string GetSummaryFileName() =>
        Environment.GetEnvironmentVariable(VariableName) ??
        throw new InvalidOperationException($"No value is set for the {VariableName} environment variable.");
}
