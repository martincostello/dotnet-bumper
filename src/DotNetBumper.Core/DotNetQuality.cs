// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing the quality of a .NET release.
/// </summary>
public enum DotNetQuality
{
    /// <summary>
    /// A daily build.
    /// </summary>
    Daily,

    /// <summary>
    /// A signed build.
    /// </summary>
    Signed,

    /// <summary>
    /// A validated build.
    /// </summary>
    Validated,

    /// <summary>
    /// A preview build.
    /// </summary>
    Preview,
}
