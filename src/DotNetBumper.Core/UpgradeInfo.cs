// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class representing information about a .NET upgrade.
/// </summary>
public sealed class UpgradeInfo
{
    /// <summary>
    /// Gets the .NET release channel.
    /// </summary>
    public required Version Channel { get; init; }

    /// <summary>
    /// Gets the .NET SDK version.
    /// </summary>
    public required NuGetVersion SdkVersion { get; init; }

    /// <summary>
    /// Gets the type of the .NET release.
    /// </summary>
    public required DotNetReleaseType ReleaseType { get; init; }
}
