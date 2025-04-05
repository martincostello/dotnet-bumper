// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// Represents the result of running a process.
/// </summary>
/// <param name="Success">Whether the process exited successfully.</param>
/// <param name="ExitCode">The exit code from the process.</param>
/// <param name="StandardOutput">The standard output from the process.</param>
/// <param name="StandardError">The standard error from the process.</param>
public record ProcessResult(
    bool Success,
    int ExitCode,
    string StandardOutput,
    string StandardError);
