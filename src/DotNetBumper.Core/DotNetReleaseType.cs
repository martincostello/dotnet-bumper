// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing a type of .NET release.
/// </summary>
public enum DotNetReleaseType
{
    /// <summary>
    /// A long-term support (LTS) release.
    /// </summary>
    Lts = 0,

    /// <summary>
    /// A standard-term support (STS) release.
    /// </summary>
    Sts,

    /// <summary>
    /// A preview release.
    /// </summary>
    Preview,
}
