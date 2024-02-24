// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

internal abstract class UpgradeTask(
    IAnsiConsole console,
    IOptions<UpgradeOptions> options,
    ILogger logger)
{
    public virtual int Order => 0;

    protected IAnsiConsole Console { get; } = console;

    protected UpgradeOptions Options => options.Value;

    protected ILogger Logger { get; } = logger;

    protected abstract string Action { get; }

    protected virtual Color ActionColor { get; } = EnvironmentHelpers.IsGitHubActions ? Color.Teal : Color.Grey;

    protected abstract string InitialStatus { get; }

    protected virtual Spinner Spinner => Spinner.Known.Dots;

    protected virtual Style? SpinnerStyle => null;

    protected virtual Color StatusColor => Color.Silver;

    protected string RelativeName(string path)
        => ProjectHelpers.RelativeName(Options.ProjectPath, path);

    protected string StatusMessage(string message)
        => $"[{StatusColor}]{message}[/]";
}
