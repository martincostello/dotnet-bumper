// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper;

internal static partial class LogReader
{
    public static async Task<IList<BumperLogEntry>> GetBuildLogsAsync(
        string logFilePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = File.OpenRead(logFilePath);

            if (stream.Length is 0)
            {
                return [];
            }

            var logs = await JsonSerializer.DeserializeAsync<BumperLog>(stream, cancellationToken: cancellationToken);
            return logs?.Entries ?? [];
        }
        catch (Exception ex)
        {
            Log.ReadMSBuildLogsFailed(logger, ex);
            return [];
        }
    }

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

                using var stream = File.OpenRead(fileName);

                if (stream.Length > 0)
                {
                    var logs = await JsonSerializer.DeserializeAsync<BumperTestLog>(stream, cancellationToken: cancellationToken) ?? new();

                    if (logs.Outcomes.Count > 0)
                    {
                        result.Outcomes = result.Outcomes.Concat(logs.Outcomes).ToDictionary();
                    }

                    if (logs.Summary.Count > 0)
                    {
                        result.Summary = result.Summary.Concat(logs.Summary).ToDictionary();
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
