// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Spectre.Console;

namespace MartinCostello.DotNetBumper;

internal static class Program
{
    public static async Task Main()
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        var upgrader = new ProjectUpgrader(AnsiConsole.Console);
        await upgrader.UpgradeAsync(cts.Token);
    }
}
