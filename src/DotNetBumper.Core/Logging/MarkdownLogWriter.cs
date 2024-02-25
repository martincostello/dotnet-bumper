﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

internal class MarkdownLogWriter(string fileName) : FileLogWriter(fileName)
{
    protected override async Task WriteLogAsync(BumperLogContext context, Stream stream, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);

        await writer.WriteLineAsync("# .NET Bumper");
        await writer.WriteLineAsync();

        if (context.DotNetSdkVersion is not null)
        {
            await writer.WriteLineAsync($"Project upgraded to .NET SDK `{context.DotNetSdkVersion}`.");
            await writer.WriteLineAsync();
        }

        if (context.Warnings.Count > 0)
        {
            await writer.WriteLineAsync("## Warnings");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("<details>");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("<summary>Warnings</summary>");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("```text");

            foreach (string warning in context.Warnings)
            {
                await writer.WriteLineAsync(warning);
            }

            await writer.WriteLineAsync("```");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("</details>");
            await writer.WriteLineAsync();
        }

        if (context.BuildLogs is not null &&
            context.BuildLogs.Summary.Sum((p) => p.Value.Count) > 0)
        {
            await writer.WriteLineAsync("## Build Summary");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("| **Type** | **ID** | **Count** |");
            await writer.WriteLineAsync("|:---------|:-------|----------:|");

            foreach ((var type, var entries) in context.BuildLogs.Summary)
            {
                string emoji = type switch
                {
                    "Error" => ":x:",
                    "Warning" => ":warning:",
                    _ => ":information_source:",
                };

                foreach ((var id, var count) in entries)
                {
                    await writer.WriteLineAsync($"| {emoji} {type} | {id} | {count} |");
                }

                await writer.WriteLineAsync();
            }

            await writer.WriteLineAsync();
        }

        if (context.TestLogs is not null &&
            context.TestLogs.Summary.Sum((p) => p.Value.Count) > 0)
        {
            await writer.WriteLineAsync("## Test Summary");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("| **Container** | **Passed** :white_check_mark: | **Failed** :x: | **Skipped** :zzz: |");
            await writer.WriteLineAsync("|:--|--:|--:|--:|");

            foreach ((var container, var entries) in context.TestLogs.Summary)
            {
                long passed = entries.TryGetValue("Passed", out var count) ? count : 0;
                long failed = entries.TryGetValue("Failed", out count) ? count : 0;
                long skipped = entries.TryGetValue("Skipped", out count) ? count : 0;

                await writer.WriteLineAsync($"| {container} | {passed} | {failed} | {skipped} |");
            }

            await writer.WriteLineAsync();
        }

        if (context.RemainingReferences.Count > 0)
        {
            await writer.WriteLineAsync("## Remaining References");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("| **Location** | **Text** |");
            await writer.WriteLineAsync("|:-------------|:---------|");

            foreach (var (project, edits) in context.RemainingReferences)
            {
                foreach (var edit in edits)
                {
                    await writer.WriteLineAsync($"| `{project.RelativePath}:{edit.Line}` | `{edit.Text}` |");
                }
            }

            await writer.WriteLineAsync();
        }
    }
}
