// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

[Command(
    Name = "dotnet bumper",
    FullName = "Upgrades projects to a newer version of .NET.")]
[VersionOptionFromMember(MemberName = nameof(GetVersion))]
internal class Program(ProjectUpgrader upgrader) : Command
{
    public static async Task<int> Main(string[] args)
    {
        using var services = new ServiceCollection()
            .AddSingleton<IAnsiConsole>(AnsiConsole.Console)
            .AddSingleton<ProjectUpgrader>()
            .BuildServiceProvider();

        using var app = new CommandLineApplication<Program>();
        app.Conventions
            .UseDefaultConventions()
            .UseConstructorInjection(services);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cts.Cancel();
        };

        return await app.ExecuteAsync(args, cts.Token);
    }

    public static string GetVersion() =>
        typeof(Program)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion;

    public async Task<int> OnExecute(CancellationToken cancellationToken)
    {
        await upgrader.UpgradeAsync(cancellationToken);
        return 0;
    }
}
