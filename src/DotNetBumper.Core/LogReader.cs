// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
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
                    result.Outcomes = result.Outcomes.Concat(logs.Outcomes).ToDictionary();
                }

                if (logs.Summary.Count > 0)
                {
                    result.Summary = result.Summary.Concat(logs.Summary).ToDictionary();
                }
            }
        }
        catch (Exception ex)
        {
            Log.ReadTestLogsFailed(logger, ex);
        }

        return result;
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
