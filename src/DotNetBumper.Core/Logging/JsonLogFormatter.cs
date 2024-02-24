﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper.Logging;

internal sealed class JsonLogFormatter(string fileName) : FileLogWriter(fileName)
{
    protected override async Task WriteLogAsync(BumperLogContext context, Stream stream, CancellationToken cancellationToken)
    {
        var document = CreateDocument(context);

        var writer = new Utf8JsonWriter(stream);

        document.WriteTo(writer);

        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static JsonObject CreateDocument(BumperLogContext context)
    {
        var root = new JsonObject()
        {
            ["startedAt"] = JsonValue.Create(context.StartedAt),
            ["finishedAt"] = JsonValue.Create(context.FinishedAt),
            ["sdkVersion"] = JsonValue.Create(context.DotNetSdkVersion),
        };

        if (context.BuildSummary is not null || context.BuildLogs is not null)
        {
            WriteBuild(root, context.BuildSummary, context.BuildLogs);
        }

        if (context.TestLogs is not null)
        {
            WriteTests(root, context.TestLogs);
        }

        if (context.PotentialEdits is not null)
        {
            var edits = new JsonObject();

            foreach ((var file, var potentialEdits) in context.PotentialEdits)
            {
                var fileEdits = new JsonArray();

                foreach (var edit in potentialEdits)
                {
                    var fileEdit = new JsonObject()
                    {
                        ["line"] = JsonValue.Create(edit.Line),
                        ["column"] = JsonValue.Create(edit.Column),
                        ["text"] = JsonValue.Create(edit.Text),
                    };

                    fileEdits.Add(fileEdit);
                }

                edits[file.RelativePath] = fileEdits;
            }

            root["potentialEdits"] = edits;
        }

        return root;
    }

    private static void WriteBuild(
        JsonObject root,
        IDictionary<string, IDictionary<string, long>>? summary,
        BumperLog? logs)
    {
        var build = new JsonObject();

        if (summary is not null)
        {
            var summarized = new JsonObject();

            foreach ((var type, var entries) in summary)
            {
                var logsForType = new JsonObject();

                foreach ((var id, var count) in entries)
                {
                    logsForType[id] = JsonValue.Create(count);
                }

                summarized[type] = logsForType;
            }

            build["summary"] = summarized;
        }

        if (logs?.Entries is not null)
        {
            var entries = new JsonArray();

            foreach (var entry in logs.Entries)
            {
                var log = new JsonObject()
                {
                    ["id"] = JsonValue.Create(entry.Id),
                    ["message"] = JsonValue.Create(entry.Message),
                    ["type"] = JsonValue.Create(entry.Type),
                };

                entries.Add(log);
            }

            build["logs"] = entries;
        }

        root["build"] = build;
    }

    private static void WriteTests(JsonObject root, BumperTestLog testLogs)
    {
        var tests = new JsonObject();

        if (testLogs.Summary?.Count > 0)
        {
            var testResults = new JsonObject();

            foreach ((var container, var outcomes) in testLogs.Summary)
            {
                var results = new JsonObject();

                foreach ((var outcome, var count) in outcomes)
                {
                    results[outcome] = JsonValue.Create(count);
                }

                testResults[container] = results;
            }

            tests["summary"] = testResults;
        }

        if (testLogs.Outcomes is not null)
        {
            var outcomes = new JsonObject();

            foreach ((var container, var entries) in testLogs.Outcomes)
            {
                var results = new JsonArray();

                foreach (var entry in entries)
                {
                    var test = new JsonObject()
                    {
                        ["id"] = JsonValue.Create(entry.Id),
                        ["message"] = JsonValue.Create(entry.Outcome),
                    };

                    results.Add(test);
                }
            }

            tests["results"] = outcomes;
        }

        root["test"] = tests;
    }
}
