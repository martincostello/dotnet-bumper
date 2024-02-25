// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.PostProcessors;

namespace MartinCostello.DotNetBumper.Logging;

/// <summary>
/// A class representing the logging context for .NET Bumper. This class cannot be inherited.
/// </summary>
public sealed class BumperLogContext
{
    /// <summary>
    /// Gets or sets the date and time the upgrade started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time the upgrade finished.
    /// </summary>
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>
    /// Gets or sets the version of the .NET SDK that was found to upgrade to, if any.
    /// </summary>
    public string? DotNetSdkVersion { get; set; }

    /// <summary>
    /// Gets the build logs, if any.
    /// </summary>
    public BumperBuildLog? BuildLogs { get; private set; }

    /// <summary>
    /// Gets the test logs, if any.
    /// </summary>
    public BumperTestLog? TestLogs { get; private set; }

    /// <summary>
    /// Gets or sets any warnings encountered during the upgrade.
    /// </summary>
    public IList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the remanining references, if any.
    /// </summary>
    internal IDictionary<ProjectFile, List<PotentialFileEdit>> RemainingReferences { get; set; } = new Dictionary<ProjectFile, List<PotentialFileEdit>>();

    /// <summary>
    /// Adds the logs from the specified <see cref="DotNetResult"/> to the logging context.
    /// </summary>
    /// <param name="result">The result to add.</param>
    public void Add(DotNetResult result)
    {
        if (result.BuildLogs is not null)
        {
            Add(result.BuildLogs);
        }

        if (result.TestLogs is not null)
        {
            Add(result.TestLogs);
        }
    }

    /// <summary>
    /// Adds the specified <see cref="BumperBuildLog"/> to the logging context.
    /// </summary>
    /// <param name="log">The build log to add.</param>
    public void Add(BumperBuildLog log)
    {
        if (BuildLogs is null)
        {
            BuildLogs = log;
        }
        else
        {
            foreach (var entry in log.Entries)
            {
                BuildLogs.Entries.Add(entry);
            }

            foreach ((string type, var entries) in log.Summary)
            {
                var summary = BuildLogs.Summary;

                if (!summary.TryGetValue(type, out var existing))
                {
                    summary[type] = existing = new Dictionary<string, long>();
                }

                foreach ((var id, var count) in entries)
                {
                    if (existing.TryGetValue(id, out var current))
                    {
                        existing[id] = current + count;
                    }
                    else
                    {
                        existing[id] = count;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds the specified <see cref="BumperTestLog"/> to the logging context.
    /// </summary>
    /// <param name="log">The test log to add.</param>
    public void Add(BumperTestLog log)
    {
        if (TestLogs is null)
        {
            TestLogs = log;
        }
        else
        {
            foreach ((var container, var entries) in log.Outcomes)
            {
                if (!TestLogs.Outcomes.TryGetValue(container, out var outcomes))
                {
                    TestLogs.Outcomes[container] = outcomes = [];
                }

                foreach (var entry in entries)
                {
                    outcomes.Add(entry);
                }
            }

            foreach ((string type, var entries) in log.Summary)
            {
                var summary = TestLogs.Summary;

                if (!summary.TryGetValue(type, out var existing))
                {
                    summary[type] = existing = new Dictionary<string, long>();
                }

                foreach ((var outcome, var count) in entries)
                {
                    if (existing.TryGetValue(outcome, out var current))
                    {
                        existing[outcome] = current + count;
                    }
                    else
                    {
                        existing[outcome] = count;
                    }
                }
            }
        }
    }
}
