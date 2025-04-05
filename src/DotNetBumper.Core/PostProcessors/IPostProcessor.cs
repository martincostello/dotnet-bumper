// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.PostProcessors;

/// <summary>
/// Defines a mechanism to run actions after upgrading a project to use a newer version of .NET.
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Runs a post-processing after an upgrade to the project.
    /// </summary>
    /// <param name="upgrade">The version of .NET to upgrade to.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>
    /// A <see cref="Task{ProcessingResult}"/> representing the result of the asynchronous operation to post-process the upgrade.
    /// </returns>
    Task<ProcessingResult> PostProcessAsync(UpgradeInfo upgrade, CancellationToken cancellationToken);
}
