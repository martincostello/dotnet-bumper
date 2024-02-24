// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing a custom .NET Bumper logger for MSBuild.
/// </summary>
public sealed class BumperLogger : Logger
{
    /// <summary>
    /// The name of the environment variable that specifies the path to the log file.
    /// </summary>
    public const string LoggerFilePathVariableName = "MartinCostello_DotNetBumper_LogFilePath";

    private const string UnknownId = "Unknown";

    private readonly List<BumperLogEntry> _logEntries = [];
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
            var output = new BumperLog()
            {
                Entries = [.._logEntries],
            };

            string json = JsonSerializer.Serialize(output);
            File.WriteAllText(_logFilePath, json);
        }
        catch (Exception)
        {
            // Ignore to not break the whole MSBuild process
        }

        _logEntries.Clear();
    }

    private void OnWarningRaised(object sender, BuildWarningEventArgs args)
    {
        var entry = new BumperLogEntry()
        {
            HelpLink = args.HelpLink,
            Id = args.Code ?? UnknownId,
            Message = args.Message,
            Type = "Warning",
        };

        _logEntries.Add(entry);
    }

    private void OnErrorRaised(object sender, BuildErrorEventArgs args)
    {
        var entry = new BumperLogEntry()
        {
            HelpLink = args.HelpLink,
            Id = args.Code ?? UnknownId,
            Message = args.Message,
            Type = "Error",
        };

        _logEntries.Add(entry);
    }
}
