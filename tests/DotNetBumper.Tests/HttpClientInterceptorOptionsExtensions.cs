// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using JustEat.HttpClientInterception;

namespace MartinCostello.DotNetBumper;

internal static class HttpClientInterceptorOptionsExtensions
{
    public static async Task<HttpClientInterceptorOptions> RegisterBundleFromResourceStreamAsync(
        this HttpClientInterceptorOptions options,
        string name,
        IEnumerable<KeyValuePair<string, string>>? templateValues = default,
        CancellationToken cancellationToken = default)
    {
        using var stream = GetStream(name);
        return await options.RegisterBundleFromStreamAsync(stream, templateValues, cancellationToken);
    }

    private static Stream GetStream(string name)
    {
        var type = typeof(HttpClientInterceptorOptionsExtensions);
        var assembly = type.Assembly;
        name = $"{type.Namespace}.Bundles.{name}.json";

        var stream = assembly.GetManifestResourceStream(name);

        return stream ?? throw new ArgumentException($"The resource '{name}' was not found.", nameof(name));
    }
}
