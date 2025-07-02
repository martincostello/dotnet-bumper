// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;

namespace MartinCostello.DotNetBumper;

internal static class DotNetPreviewFixture
{
    private static (bool HasPreview, string Channel)? _latest;

    public static async Task<bool> HasPreviewAsync()
    {
        var (hasPreview, _) = await DotNetHasPreviewAsync();
        return hasPreview;
    }

    public static async Task<string> LatestChannelAsync()
    {
        (_, var channel) = await DotNetHasPreviewAsync();
        return channel;
    }

    private static async Task<(bool HasPreview, string Channel)> DotNetHasPreviewAsync()
    {
        if (_latest is null)
        {
            string? latestChannel = null;

            using var client = new HttpClient();
            using var index = await client.GetFromJsonAsync<JsonDocument>("https://raw.githubusercontent.com/dotnet/core/refs/heads/main/release-notes/releases-index.json");

            bool hasPreview = false;

            if (index?.RootElement.TryGetProperty("releases-index", out var releases) is true)
            {
                foreach (var release in releases.EnumerateArray())
                {
                    latestChannel = release.GetProperty("channel-version").GetString();

                    if (release.TryGetProperty("support-phase", out var supportPhase) &&
                        supportPhase.ValueKind is JsonValueKind.String &&
                        supportPhase.GetString() is "go-live" or "preview")
                    {
                        hasPreview = true;
                        break;
                    }
                }
            }

            _latest = (hasPreview, latestChannel!);
        }

        return _latest.Value;
    }
}
