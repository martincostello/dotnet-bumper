// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections;
using MartinCostello.DotNetBumper;
using Microsoft.Extensions.Logging;
using Spectre.Console;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            Console.WriteLine($"[env] {entry.Key}={entry.Value}");
        }

        using var progress = TerminalProgress.Create();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        return await Bumper.RunAsync(
            AnsiConsole.Console,
            args,
            (builder) => builder.AddConsole(),
            cts.Token);
    }

    private sealed class TerminalProgress : IDisposable
    {
        //// See https://learn.microsoft.com/windows/terminal/tutorials/progress-bar-sequences

        private TerminalProgress()
            => Console.Write($"\x1b]9;4;3;0\x07");

        public static TerminalProgress? Create()
            => OperatingSystem.IsWindows() ? new TerminalProgress() : null;

        public void Dispose()
            => Console.Write("\x1b]9;4;0;0\x07");
    }
}
