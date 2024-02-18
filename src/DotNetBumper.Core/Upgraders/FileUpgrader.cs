// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal abstract partial class FileUpgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger logger) : IUpgrader
{
    protected IAnsiConsole Console => console;

    protected UpgradeOptions Options => options.Value;

    protected ILogger Logger => logger;

    protected abstract IReadOnlyList<string> Patterns { get; }

    protected virtual SearchOption SearchOption => SearchOption.AllDirectories;

    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
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

        return await UpgradeCoreAsync(upgrade, fileNames, cancellationToken);
    }

    protected abstract Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken);
}
