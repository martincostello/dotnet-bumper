// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgrades;

internal abstract partial class Upgrader(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger logger) : IUpgrader
{
    public virtual int Priority => 0;

    protected IAnsiConsole Console => console;

    protected UpgradeOptions Options => options.Value;

    protected ILogger Logger => logger;

    protected abstract string Action { get; }

    protected virtual string ActionColor => "grey";

    protected abstract string InitialStatus { get; }

    protected virtual string StatusColor => "silver";

    public virtual async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        console.MarkupLineInterpolated($"[{ActionColor}]{Action}...[/]");

        return await console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[{StatusColor}]{InitialStatus}...[/]", async (context) => await UpgradeCoreAsync(upgrade, context, cancellationToken));
    }

    protected string RelativeName(string path)
        => ProjectHelpers.RelativeName(Options.ProjectPath, path);

    protected string StatusMessage(string message)
        => $"[{StatusColor}]{message}[/]";

    protected abstract Task<bool> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        StatusContext context,
        CancellationToken cancellationToken);
}
