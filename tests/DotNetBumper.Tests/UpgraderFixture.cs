// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Logging;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectre.Console;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper;

internal sealed class UpgraderFixture(
    ITestOutputHelper outputHelper,
    IEnvironment? environment = null) : IDisposable, ITestOutputHelperAccessor
{
    private readonly TestConsole _console = new();
    private readonly Project _project = new();

    public IAnsiConsole Console => _console;

    public IEnvironment Environment => environment ??= CreateEnvironment();

    public BumperLogContext LogContext { get; } = new();

    public Project Project => _project;

    public ITestOutputHelper? OutputHelper
    {
        get => outputHelper;
        set => throw new NotSupportedException();
    }

    public BumperConfiguration UserConfiguration { get; } = new();

    public ILogger<T> CreateLogger<T>()
        => outputHelper.ToLogger<T>();

    public IOptions<UpgradeOptions> CreateOptions()
        => Options.Create(new UpgradeOptions() { ProjectPath = Project.DirectoryName });

    public void Dispose()
    {
        if (_console is { })
        {
            outputHelper.WriteLine(string.Empty);
            outputHelper.WriteLine(_console.Output);
            _console.Dispose();
        }

        _project?.Dispose();
    }

    private static IEnvironment CreateEnvironment()
    {
        var environment = Substitute.For<IEnvironment>();

        // Use values that give the most coverage
        environment.IsGitHubActions.Returns(false);
        environment.SupportsLinks.Returns(true);

        return environment;
    }
}
