// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.DotNetBumper.Upgraders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class containing extension methods to configure <see cref="ProjectUpgrader"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly (string Product, string Version) UserAgent = ("dotnet-bumper", ProjectUpgrader.Version);

    /// <summary>
    /// Adds the project upgrader to the specified service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
    /// <param name="configuration">The <see cref="IConfiguration"/> to use.</param>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="configureLogging">A delgate to a method to use to configure logging.</param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> specified by <paramref name="services"/>.
    /// </returns>
    public static IServiceCollection AddProjectUpgrader(
        this IServiceCollection services,
        IConfiguration configuration,
        IAnsiConsole console,
        Action<ILoggingBuilder> configureLogging)
    {
        services.AddLogging(configureLogging);
        services.AddOptions<UpgradeOptions>();

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton(configuration)
                .AddSingleton<DotNetProcess>()
                .AddSingleton<IAnsiConsole>(console)
                .AddSingleton<IValidateOptions<UpgradeOptions>, UpgradeOptionsValidator>()
                .AddSingleton<ProjectUpgrader>();

        services.AddHttpClient()
                .ConfigureHttpClientDefaults((options) =>
                {
                    options.ConfigureHttpClient((client) => client.DefaultRequestHeaders.UserAgent.Add(new(UserAgent.Product, UserAgent.Version)));
                    options.AddStandardResilienceHandler();
                });

        services.AddHttpClient<DotNetUpgradeFinder>();

        services.AddUpgraders();

        return services;
    }

    private static IServiceCollection AddUpgraders(this IServiceCollection services)
    {
        services.AddSingleton<IUpgrader, AwsLambdaToolsUpgrader>();
        services.AddSingleton<IUpgrader, DockerfileUpgrader>();
        services.AddSingleton<IUpgrader, GlobalJsonUpgrader>();
        services.AddSingleton<IUpgrader, PackageVersionUpgrader>();
        services.AddSingleton<IUpgrader, ServerlessUpgrader>();
        services.AddSingleton<IUpgrader, TargetFrameworkUpgrader>();
        services.AddSingleton<IUpgrader, VisualStudioCodeUpgrader>();
        services.AddSingleton<IUpgrader, VisualStudioComponentUpgrader>();

        return services;
    }
}
