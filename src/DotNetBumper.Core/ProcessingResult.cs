// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing the result of an operation.
/// </summary>
public enum ProcessingResult
{
    /// <summary>
    /// Nothing occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// One or more operations were successful.
    /// </summary>
    Success,

    /// <summary>
    /// One or more warnings occurred.
    /// </summary>
    Warning,

    /// <summary>
    /// One or more errors occurred.
    /// </summary>
    Error,
}
