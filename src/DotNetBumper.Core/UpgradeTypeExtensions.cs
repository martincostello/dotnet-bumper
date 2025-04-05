// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class containing extension methods for <see cref="UpgradeType"/>. This class cannot be inherited.
/// </summary>
public static class UpgradeTypeExtensions
{
    /// <summary>
    /// Returns whether the specified <see cref="UpgradeType"/> is a prerelease version.
    /// </summary>
    /// <param name="type">The <see cref="UpgradeType"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the <see cref="UpgradeType"/> is a prerelease version; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsPrerelease(this UpgradeType type)
        => type is UpgradeType.Daily or UpgradeType.Preview;
}
