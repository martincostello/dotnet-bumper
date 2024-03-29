﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal abstract class FileUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    ILogger logger) : Upgrader(console, environment, options, logger)
{
    protected abstract IReadOnlyList<string> Patterns { get; }

    protected virtual SearchOption SearchOption => SearchOption.AllDirectories;

    protected virtual IReadOnlyList<string> FindFiles()
    {
        List<string> fileNames = [];

        foreach (string pattern in Patterns)
        {
            fileNames.AddRange(Directory.GetFiles(Options.ProjectPath, pattern, SearchOption));
        }

        return fileNames;
    }

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        var fileNames = FindFiles();

        if (fileNames.Count == 0)
        {
            return ProcessingResult.None;
        }

        return await UpgradeCoreAsync(upgrade, fileNames, context, cancellationToken);
    }

    protected abstract Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken);
}
