// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper.Upgraders;

internal partial class ContainerRegistryClient(
    HttpClient client,
    ILogger<ContainerRegistryClient> logger)
{
    private static readonly MediaTypeWithQualityHeaderValue ManifestList = new("application/vnd.docker.distribution.manifest.list.v2+json");
    private static readonly ConcurrentDictionary<string, string> DigestCache = [];

    private string? _token;

    public virtual async Task<string?> GetImageDigestAsync(
        string image,
        string tag,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{image}:{tag}";

        if (DigestCache.TryGetValue(cacheKey, out var digest))
        {
            return digest;
        }

        (var registry, image) = ParseImage(image);

        var token = _token;

        (digest, var wwwAuthenticate) = await GetManifestDigestAsync(registry, image, tag, token, cancellationToken);

        if (digest is not null)
        {
            Log.LatestManifestDigest(logger, image, tag, digest);
            return digest;
        }

        if (wwwAuthenticate is null)
        {
            return null;
        }

        token = await GetRegistryTokenAsync(registry, wwwAuthenticate, cancellationToken);

        if (token is null)
        {
            return null;
        }

        (digest, _) = await GetManifestDigestAsync(registry, image, tag, token, cancellationToken);

        if (digest is not null)
        {
            Log.LatestManifestDigest(logger, image, tag, digest);

            DigestCache[cacheKey] = digest;
            _token = token;
        }

        return digest;
    }

    private static (string Registry, string Name) ParseImage(string container)
    {
        const char Separator = '/';
        string[] parts = container.Split(Separator, StringSplitOptions.None);

        if (parts.Length > 1 && parts[0].Contains('.', StringComparison.Ordinal))
        {
            return (parts[0], string.Join(Separator, parts.Skip(1)));
        }

        return ("registry.hub.docker.com", container);
    }

    private async Task<(string? Digest, string? WwwAuthenticate)> GetManifestDigestAsync(
        string registry,
        string image,
        string tag,
        string? token,
        CancellationToken cancellationToken)
    {
        var manifestUrl = $"https://{registry}/v2/{image}/manifests/{tag}";

        using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
        request.Headers.Accept.Add(ManifestList);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && token is null)
        {
            Log.RequestToRegistryUnauthorized(logger, registry);

            var wwwAuthenticate = response.Headers.WwwAuthenticate.FirstOrDefault();
            return (null, wwwAuthenticate?.Parameter);
        }

        string? digest = null;

        if (response.IsSuccessStatusCode && response.Headers.TryGetValues("Docker-Content-Digest", out var digests))
        {
            digest = digests.FirstOrDefault();
        }

        return (digest, null);
    }

    private async Task<string?> GetRegistryTokenAsync(
        string registry,
        string parameter,
        CancellationToken cancellationToken)
    {
        var parts = parameter.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select((p) => p.Trim().Split('='))
            .Where((p) => p.Length == 2)
            .ToDictionary((p) => p[0].Trim(), (p) => p[1].Trim('"'));

        if (parts.TryGetValue("realm", out var realm) &&
            parts.TryGetValue("service", out var service) &&
            parts.TryGetValue("scope", out var scope))
        {
            var requestUri = $"{realm}?service={service}&scope={scope}";

            using var response = await client.GetAsync(requestUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (document.RootElement.TryGetProperty("token", out var token) &&
                    token.ValueKind is JsonValueKind.String)
                {
                    Log.GotRegistryToken(logger, realm);
                    return token.GetString();
                }
            }
            else
            {
                Log.FailedToGetRegistryToken(logger, realm, response.StatusCode);
            }
        }
        else
        {
            Log.FailedToParseWwwAuthenticate(logger, registry, parameter);
        }

        return null;
    }

    [ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Retrieved latest digest for container {Container} with tag {Tag}: {Digest}.")]
        public static partial void LatestManifestDigest(ILogger logger, string container, string tag, string digest);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Request to container registry {Registry} was unauthorized.")]
        public static partial void RequestToRegistryUnauthorized(ILogger logger, string registry);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Successfully obtained token for realm {Realm}.")]
        public static partial void GotRegistryToken(ILogger logger, string realm);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Failed to get token for realm {Realm}. HTTP status code: {StatusCode}.")]
        public static partial void FailedToGetRegistryToken(ILogger logger, string realm, HttpStatusCode statusCode);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Failed to parse WWW-Authenticate parameter from registry {Registry}. Value: {Value}")]
        public static partial void FailedToParseWwwAuthenticate(ILogger logger, string registry, string value);
    }
}
