// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal abstract partial class FileUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger logger) : Upgrader(console, options, logger)
{
    protected abstract IReadOnlyList<string> Patterns { get; }

    protected virtual SearchOption SearchOption => SearchOption.AllDirectories;

    protected override async Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        List<string> fileNames = [];

        foreach (string pattern in Patterns)
        {
            fileNames.AddRange(Directory.GetFiles(Options.ProjectPath, pattern, SearchOption));
        }

        if (fileNames.Count == 0)
        {
            return false;
        }

        return await UpgradeCoreAsync(upgrade, fileNames, context, cancellationToken);
    }

    protected abstract Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken);
}
