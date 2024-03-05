// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper;

internal sealed partial class BumperConfigurationProvider(
    BumperConfigurationLoader loader,
    IOptions<UpgradeOptions> options,
    ILogger<BumperConfigurationProvider> logger)
{
    private BumperConfiguration? _configuration;

    public async Task<BumperConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        return _configuration ??= await InitializeAsync(cancellationToken);
    }

    private async Task<BumperConfiguration> InitializeAsync(CancellationToken cancellationToken)
    {
        var configuration = new BumperConfiguration()
        {
            IncludeNuGetPackages =
            {
                "Microsoft.AspNetCore.",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions.",
                "System.Text.Json",
            },
        };

        if (options.Value.UpgradeType is UpgradeType.Preview)
        {
            configuration.NoWarn.Add("NETSDK1057");
            configuration.NoWarn.Add("NU5104");
        }
        else
        {
            configuration.IncludeNuGetPackages.Add("Microsoft.NET.Test.Sdk");
        }

        // HACK See https://github.com/dotnet-outdated/dotnet-outdated/pull/516
        configuration.NoWarn.Add("NU1605");

        if (await loader.LoadAsync(cancellationToken) is { } custom)
        {
            configuration.IncludeNuGetPackages.UnionWith(custom.IncludeNuGetPackages);
            configuration.ExcludeNuGetPackages.UnionWith(custom.ExcludeNuGetPackages);
            configuration.NoWarn.UnionWith(custom.NoWarn);
            configuration.RemainingReferencesIgnore.UnionWith(custom.RemainingReferencesIgnore);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            Log.IncludedNuGetPackages(logger, [..configuration.IncludeNuGetPackages]);
            Log.ExcludedNuGetPackages(logger, [..configuration.ExcludeNuGetPackages]);
            Log.NoWarn(logger, [..configuration.NoWarn]);
            Log.IgnoreRemainingReferences(logger, [..configuration.RemainingReferencesIgnore]);
        }

        return configuration;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Included update NuGet packages: {Packages}",
            SkipEnabledCheck = true)]
        public static partial void IncludedNuGetPackages(ILogger logger, string[] packages);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Excluded update NuGet packages: {Packages}",
            SkipEnabledCheck = true)]
        public static partial void ExcludedNuGetPackages(ILogger logger, string[] packages);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Ignored MSBuild warnings: {NoWarn}",
            SkipEnabledCheck = true)]
        public static partial void NoWarn(ILogger logger, string[] noWarn);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Ignored remaining references: {References}",
            SkipEnabledCheck = true)]
        public static partial void IgnoreRemainingReferences(ILogger logger, string[] references);
    }
}
