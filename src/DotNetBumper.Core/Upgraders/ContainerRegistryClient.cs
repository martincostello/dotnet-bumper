// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace MartinCostello.DotNetBumper.Upgraders;

internal partial class ContainerRegistryClient(
    HttpClient client,
    ILogger<ContainerRegistryClient> logger)
{
    private static readonly MediaTypeWithQualityHeaderValue ManifestList = new("application/vnd.docker.distribution.manifest.list.v2+json");
    private static readonly MediaTypeWithQualityHeaderValue Manifest = new("application/vnd.docker.distribution.manifest.v2+json");
    private static readonly MediaTypeWithQualityHeaderValue OciManifest = new("application/vnd.oci.image.manifest.v1+json");
    private static readonly MediaTypeWithQualityHeaderValue OciIndex = new("application/vnd.oci.image.index.v1+json");

    private static readonly ConcurrentDictionary<string, string> DigestCache = [];

    private AuthenticationHeaderValue? _authorization;

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

        var authorization = _authorization;

        (digest, var wwwAuthenticate) = await GetDigestAsync(registry, image, tag, authorization, cancellationToken);

        if (digest is not null)
        {
            Log.LatestManifestDigest(logger, image, tag, digest);
            return digest;
        }

        if (wwwAuthenticate is null || wwwAuthenticate.Parameter is null)
        {
            return null;
        }

        authorization = await GetRegistryAuthorizationAsync(registry, wwwAuthenticate, cancellationToken);

        (digest, _) = await GetDigestAsync(registry, image, tag, authorization, cancellationToken);

        if (digest is not null)
        {
            Log.LatestManifestDigest(logger, image, tag, digest);

            DigestCache[cacheKey] = digest;
            _authorization = authorization;
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

    private async Task<(string? Digest, AuthenticationHeaderValue? WwwAuthenticate)> GetDigestAsync(
        string registry,
        string image,
        string tag,
        AuthenticationHeaderValue? authorization,
        CancellationToken cancellationToken)
    {
        // Based on https://github.com/renovatebot/renovate/blob/4f7c26cdf7edcecde68a9fd196273143d563ecc3/lib/modules/datasource/docker/index.ts
        var manifestUrl = $"https://{registry}/v2/{image}/manifests/{tag}";

        using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);

        request.Headers.Accept.Add(ManifestList);
        request.Headers.Accept.Add(Manifest);
        request.Headers.Accept.Add(OciManifest);
        request.Headers.Accept.Add(OciIndex);

        if (authorization is not null)
        {
            request.Headers.Authorization = authorization;
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && authorization is null)
        {
            Log.RequestToRegistryUnauthorized(logger, registry);
            return (null, response.Headers.WwwAuthenticate.FirstOrDefault());
        }

        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        if (response.Headers.TryGetValues("Docker-Content-Digest", out var digests))
        {
            return (digests.FirstOrDefault(), null);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = await SHA256.HashDataAsync(stream, cancellationToken);

#if NET9_0_OR_GREATER
        var digest = Convert.ToHexStringLower(buffer);
#else
#pragma warning disable CA1308
        var digest = Convert.ToHexString(buffer);
#pragma warning restore CA1308
#endif

        return ($"sha256:{digest}", null);
    }

    private async Task<AuthenticationHeaderValue> GetRegistryAuthorizationAsync(
        string registry,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authorization.Parameter))
        {
            return authorization;
        }

        var parts = authorization.Parameter.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select((p) => p.Trim().Split('='))
            .Where((p) => p.Length == 2)
            .ToDictionary((p) => p[0].Trim(), (p) => p[1].Trim('"'));

        // Based on https://github.com/renovatebot/renovate/blob/main/lib/modules/datasource/docker/common.ts#L154-L159
        if (parts.TryGetValue("realm", out var realm) &&
            Uri.TryCreate(realm, UriKind.Absolute, out _))
        {
            var requestUri = realm;

            if (parts.TryGetValue("service", out var service))
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, "service", service);
            }

            if (parts.TryGetValue("scope", out var scope))
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, "scope", scope);
            }

            using var response = await client.GetAsync(requestUri, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if ((document.RootElement.TryGetProperty("token", out var token) ||
                     document.RootElement.TryGetProperty("access_token", out token)) &&
                    token.ValueKind is JsonValueKind.String)
                {
                    Log.GotAuthorizationForRealm(logger, realm);
                    return new(authorization.Scheme, token.GetString());
                }
            }
            else
            {
                Log.FailedToGetAuthorization(logger, realm, response.StatusCode);
            }
        }
        else
        {
            Log.UnableToParseWwwAuthenticate(logger, registry, authorization.Parameter);
        }

        return authorization;
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
            Message = "Successfully obtained authorization for realm {Realm}.")]
        public static partial void GotAuthorizationForRealm(ILogger logger, string realm);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Failed to get token for realm {Realm}. HTTP status code: {StatusCode}.")]
        public static partial void FailedToGetAuthorization(ILogger logger, string realm, HttpStatusCode statusCode);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Unable to parse WWW-Authenticate parameter for realm from registry {Registry}. Value: {Value}")]
        public static partial void UnableToParseWwwAuthenticate(ILogger logger, string registry, string value);
    }
}
