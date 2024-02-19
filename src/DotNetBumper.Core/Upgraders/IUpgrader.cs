// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Upgraders;

/// <summary>
/// Defines a mechanism to upgrade a project in one or more ways to use a newer version of .NET.
/// </summary>
public interface IUpgrader
{
    /// <summary>
    /// Gets the global priority of the upgrader.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Attempts to apply an upgrade to the project.
    /// </summary>
    /// <param name="upgrade">The version of .NET to upgrade to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{UpgradeResult}"/> representing the result of the asynchronous operation to upgrade the project.
    /// </returns>
    Task<UpgradeResult> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken);
}
