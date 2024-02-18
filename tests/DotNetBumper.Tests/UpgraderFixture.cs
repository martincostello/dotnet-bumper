// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;
using Spectre.Console;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper;

internal sealed class UpgraderFixture(ITestOutputHelper outputHelper) : IDisposable, ITestOutputHelperAccessor
{
    private readonly TestConsole _console = new();
    private readonly Project _project = new();

    public IAnsiConsole Console => _console;

    public Project Project => _project;

    public ITestOutputHelper? OutputHelper
    {
        get => outputHelper;
        set => throw new NotSupportedException();
    }

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
}
