// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.PostProcessors;
using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Testing;

namespace MartinCostello.DotNetBumper;

public static class ServiceCollectionExtensionsTests
{
    [Fact]
    public static void AddProjectUpgrader_Registers_All_Upgraders()
    {
        // Arrange, Act and Assert
        AddProjectUpgraderRegistersAllImplementations<IUpgrader>();
    }

    [Fact]
    public static void AddProjectUpgrader_Registers_All_PostProcessors()
    {
        // Arrange, Act and Assert
        AddProjectUpgraderRegistersAllImplementations<IPostProcessor>();
    }

    private static void AddProjectUpgraderRegistersAllImplementations<T>()
        where T : notnull
    {
        // Arrange
        var expected = typeof(T).Assembly
            .GetTypes()
            .Where((p) => typeof(T).IsAssignableFrom(p))
            .Where((p) => !p.IsAbstract)
            .Where((p) => !p.IsInterface)
            .ToArray();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        using var console = new TestConsole();

        // Act
        services.AddProjectUpgrader(configuration, console, (_) => { });

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var actual = serviceProvider.GetServices<T>();

        actual.ShouldNotBeNull();
        actual.Select((p) => p.GetType())
              .ToArray()
              .ShouldBe(expected, ignoreOrder: true);
    }
}
