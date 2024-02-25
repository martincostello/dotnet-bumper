// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing a custom .NET Bumper logger for VSTest.
/// </summary>
[ExtensionUri(ExtensionUri)]
[FriendlyName(FriendlyName)]
public sealed class BumperTestLogger : ITestLogger
{
    /// <summary>
    /// The extension URI of the test logger.
    /// </summary>
    public const string ExtensionUri = "logger://martincostello/dotnet-bumper/test-logger";

    /// <summary>
    /// The friendly name of the test logger.
    /// </summary>
    public const string FriendlyName = "MartinCostello.DotNetBumper";

    /// <summary>
    /// The name of the environment variable that specifies the path to the log file.
    /// </summary>
    public const string LoggerDirectoryPathVariableName = "MartinCostello_DotNetBumper_TestLogPath";

    private const string DefaultContainerName = "Default";

    private readonly ConcurrentDictionary<string, IList<BumperTestLogEntry>> _logEntries = [];

    private string? _logDirectoryPath;
    private string _testContainer = DefaultContainerName;

    /// <inheritdoc />
    public void Initialize(TestLoggerEvents events, string testRunDirectory)
        => InitializeLogger(events, Environment.GetEnvironmentVariable(LoggerDirectoryPathVariableName));

    /// <summary>
    /// Initializes the logger with the specified event source and log file path.
    /// </summary>
    /// <param name="events">The <see cref="TestLoggerEvents"/> to use.</param>
    /// <param name="logFilePath">The path of the log file to use.</param>
    public void InitializeLogger(TestLoggerEvents events, string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new InvalidOperationException($"The {LoggerDirectoryPathVariableName} environment variable has no value set.");
        }

        _logDirectoryPath = logFilePath;

        events.TestRunStart += OnTestRunStart;
        events.TestResult += OnTestResult;
        events.TestRunComplete += OnTestRunComplete;
    }

    private void OnTestRunStart(object sender, TestRunStartEventArgs args)
    {
        _testContainer = args.TestRunCriteria.Sources
            .Select((p) => Path.GetFileNameWithoutExtension(p))
            .DefaultIfEmpty(DefaultContainerName)
            .FirstOrDefault();
    }

    private void OnTestResult(object sender, TestResultEventArgs args)
    {
        if (!_logEntries.TryGetValue(_testContainer, out var entries))
        {
            _logEntries[_testContainer] = entries = [];
        }

        var entry = new BumperTestLogEntry()
        {
            Id = args.Result.DisplayName,
            Outcome = args.Result.Outcome.ToString(),
            ErrorMessage = args.Result.ErrorMessage,
        };

        entries.Add(entry);
    }

    private void OnTestRunComplete(object sender, TestRunCompleteEventArgs args)
    {
        try
        {
            var testLogs = new BumperTestLog();

            if (_logEntries.TryGetValue(_testContainer, out var entries))
            {
                var outcomes = new List<BumperTestLogEntry>(entries.Count);

                foreach (var entry in entries)
                {
                    outcomes.Add(entry);
                }

                testLogs.Outcomes[_testContainer] = outcomes;
            }

            var summary = new Dictionary<string, long>(args.TestRunStatistics.Stats.Count);

            foreach (var pair in args.TestRunStatistics.Stats)
            {
                string key = pair.Key.ToString();

                if (!summary.TryGetValue(key, out var count))
                {
                    count = 0;
                }

                summary[key] = pair.Value + count;
            }

            testLogs.Summary[_testContainer] = summary;

            string json = JsonSerializer.Serialize(testLogs);
            string fileName = Path.Combine(_logDirectoryPath, Path.GetRandomFileName() + ".json");

            File.WriteAllText(fileName, json);

            _logEntries[_testContainer].Clear();
            _testContainer = DefaultContainerName;
        }
        catch (Exception)
        {
            // Ignore to not break the whole test process
        }
    }
}
