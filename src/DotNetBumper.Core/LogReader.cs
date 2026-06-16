// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper;

internal static partial class LogReader
{
    public static async Task<BumperBuildLog> GetBuildLogsAsync(
        string logFilePath,
        ILogger logger,
        CancellationToken cancellationToken)
        => await DeserializeAsync<BumperBuildLog>(logFilePath, (ex) => Log.ReadMSBuildLogsFailed(logger, ex), cancellationToken);

    public static async Task<BumperTestLog> GetTestLogsAsync(
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = new BumperTestLog();

        try
        {
            foreach (var fileName in Directory.EnumerateFiles(path, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var logs = await DeserializeAsync<BumperTestLog>(fileName, (ex) => Log.ReadTestLogsFailed(logger, ex), cancellationToken);

                if (logs.Outcomes.Count > 0)
                {
                    foreach ((var container, var outcomes) in logs.Outcomes)
                    {
                        result.Outcomes[container] =
                            result.Outcomes.TryGetValue(container, out var existing) ?
                            [.. existing, .. outcomes] :
                            outcomes;
                    }
                }

                if (logs.Summary.Count > 0)
                {
                    foreach ((var container, var summary) in logs.Summary)
                    {
                        if (!result.Summary.TryGetValue(container, out var existing))
                        {
                            existing = summary;
                        }
                        else
                        {
                            foreach ((var outcome, var count) in summary)
                            {
                                if (!existing.TryGetValue(outcome, out var existingCount))
                                {
                                    existingCount = 0;
                                }

                                existing[outcome] = existingCount + count;
                            }
                        }

                        result.Summary[container] = existing;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.ReadTestLogsFailed(logger, ex);
        }

        return result;
    }

    public static async Task<BumperTestLog> GetTestLogsFromTrxAsync(
        string path,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = new BumperTestLog();

        try
        {
            foreach (var fileName in Directory.EnumerateFiles(path, "*.trx"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var stream = File.OpenRead(fileName);
                    var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

                    ParseTrx(document, result);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log.ReadTestLogsFailed(logger, ex);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.ReadTestLogsFailed(logger, ex);
        }

        return result;
    }

    private static void ParseTrx(XDocument document, BumperTestLog result)
    {
        // See https://learn.microsoft.com/visualstudio/test/test-result-trx-format
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        var testRun = document.Root;

        if (testRun is null)
        {
            return;
        }

        // Each TRX file is produced by a single test application (one per project and target
        // framework), so derive a container name from the test assembly to keep the results distinct.
        var container = testRun
            .Element(ns + "TestDefinitions")?
            .Elements(ns + "UnitTest")
            .Select((p) => p.Attribute("storage")?.Value)
            .FirstOrDefault((p) => !string.IsNullOrEmpty(p));

        container = string.IsNullOrEmpty(container) ? "Default" : Path.GetFileNameWithoutExtension(container);

        var results = testRun.Element(ns + "Results");

        if (results is not null)
        {
            var outcomes = new List<BumperTestLogEntry>();

            foreach (var unitTestResult in results.Elements(ns + "UnitTestResult"))
            {
                outcomes.Add(new()
                {
                    Id = unitTestResult.Attribute("testName")?.Value,
                    Outcome = MapOutcome(unitTestResult.Attribute("outcome")?.Value),
                    ErrorMessage = unitTestResult
                        .Element(ns + "Output")?
                        .Element(ns + "ErrorInfo")?
                        .Element(ns + "Message")?.Value,
                });
            }

            if (outcomes.Count > 0)
            {
                result.Outcomes[container] =
                    result.Outcomes.TryGetValue(container, out var existing) ?
                    [.. existing, .. outcomes] :
                    outcomes;
            }
        }

        var counters = testRun.Element(ns + "ResultSummary")?.Element(ns + "Counters");

        if (counters is not null)
        {
            var summary = new Dictionary<string, long>()
            {
                ["Passed"] = GetCount(counters, "passed"),
                ["Skipped"] = GetCount(counters, "notExecuted") + GetCount(counters, "inconclusive"),
                ["Failed"] = GetCount(counters, "failed") +
                             GetCount(counters, "error") +
                             GetCount(counters, "timeout") +
                             GetCount(counters, "aborted"),
            };

            if (result.Summary.TryGetValue(container, out var existing))
            {
                foreach ((var outcome, var count) in summary)
                {
                    existing.TryGetValue(outcome, out var current);
                    existing[outcome] = current + count;
                }
            }
            else
            {
                result.Summary[container] = summary;
            }
        }

        static long GetCount(XElement counters, string name)
            => long.TryParse(counters.Attribute(name)?.Value, out var value) ? value : 0;

        static string MapOutcome(string? outcome) => outcome switch
        {
            "Passed" => "Passed",
            "NotExecuted" or "Inconclusive" => "Skipped",
            null or "" => "None",
            _ => "Failed",
        };
    }

    private static async Task<T> DeserializeAsync<T>(string path, Action<Exception> onException, CancellationToken cancellationToken)
        where T : class, new()
    {
        T? result = null;

        try
        {
            using var stream = File.OpenRead(path);

            if (stream.Length > 0)
            {
                result = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            onException(ex);
        }

        return result ?? new();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed to read MSBuild logs.")]
        public static partial void ReadMSBuildLogsFailed(ILogger logger, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed to read dotnet test logs.")]
        public static partial void ReadTestLogsFailed(ILogger logger, Exception exception);
    }
}
