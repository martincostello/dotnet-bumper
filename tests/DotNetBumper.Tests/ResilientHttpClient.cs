// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.DotNetBumper;

internal static class ResilientHttpClient
{
    private static readonly Lazy<IHttpClientFactory> Factory = new(CreateFactory);

    public static HttpClient Create()
        => Factory.Value.CreateClient(nameof(ResilientHttpClient));

    private static IHttpClientFactory CreateFactory()
    {
        var services = new ServiceCollection();

        services.AddHttpClient(nameof(ResilientHttpClient))
                .AddStandardResilienceHandler();

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }
}
