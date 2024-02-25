// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing a custom .NET Bumper logger for MSBuild.
/// </summary>
public sealed class BumperBuildLogger : Logger
{
    /// <summary>
    /// The name of the environment variable that specifies the path to the log file.
    /// </summary>
    public const string LoggerFilePathVariableName = "MartinCostello_DotNetBumper_BuildLogPath";

    private const string UnknownId = "Unknown";

    private readonly List<BumperBuildLogEntry> _logEntries = [];
    private string? _logFilePath;

    /// <inheritdoc />
    public override void Initialize(IEventSource eventSource)
        => Initialize(eventSource, Environment.GetEnvironmentVariable(LoggerFilePathVariableName));

    /// <summary>
    /// Initializes the logger with the specified event source and log file path.
    /// </summary>
    /// <param name="eventSource">The <see cref="IEventSource"/> to use.</param>
    /// <param name="logFilePath">The path of the log file to use.</param>
    public void Initialize(IEventSource eventSource, string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new InvalidOperationException($"The {LoggerFilePathVariableName} environment variable has no value set.");
        }

        _logFilePath = logFilePath;

        eventSource.ErrorRaised += OnErrorRaised;
        eventSource.WarningRaised += OnWarningRaised;
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        try
        {
            BumperBuildLog log = CreateBuildLog();
            string json = JsonSerializer.Serialize(log);

            File.WriteAllText(_logFilePath, json);
        }
        catch (Exception)
        {
            // Ignore to not break the whole MSBuild process
        }

        _logEntries.Clear();
    }

    private static string Id(string? code) => code ?? UnknownId;

    private void OnWarningRaised(object sender, BuildWarningEventArgs args)
    {
        var entry = new BumperBuildLogEntry()
        {
            HelpLink = args.HelpLink,
            Id = Id(args.Code),
            Message = args.Message,
            Type = "Warning",
        };

        _logEntries.Add(entry);
    }

    private void OnErrorRaised(object sender, BuildErrorEventArgs args)
    {
        var entry = new BumperBuildLogEntry()
        {
            HelpLink = args.HelpLink,
            Id = Id(args.Code),
            Message = args.Message,
            Type = "Error",
        };

        _logEntries.Add(entry);
    }

    private BumperBuildLog CreateBuildLog()
    {
        var log = new BumperBuildLog()
        {
            Entries = [.. _logEntries],
        };

        var summary = new Dictionary<string, IDictionary<string, long>>();

        foreach (var group in log.Entries.GroupBy((p) => p.Type).OrderBy((p) => p.Key))
        {
            var grouped = new Dictionary<string, long>();

            foreach (var entries in group.GroupBy((p) => p.Id).OrderBy((p) => p.Key))
            {
                grouped[entries.Key!] = entries.Count();
            }

            summary[group.Key!] = new Dictionary<string, long>();
        }

        return log;
    }
}
