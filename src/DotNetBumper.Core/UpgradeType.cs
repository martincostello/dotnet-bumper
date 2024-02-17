// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing a type of .NET upgrade to perform.
/// </summary>
public enum UpgradeType
{
    /// <summary>
    /// Upgrade to the latest Long Term Support (LTS) version.
    /// </summary>
    Lts = 0,

    /// <summary>
    /// Upgrade to the latest stable version.
    /// </summary>
    Latest,

    /// <summary>
    /// Upgrade to the latest preview release.
    /// </summary>
    Preview,
}
