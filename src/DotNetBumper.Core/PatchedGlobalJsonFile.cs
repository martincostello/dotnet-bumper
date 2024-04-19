// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

internal sealed class PatchedGlobalJsonFile : IDisposable
{
    private readonly string _filePath;
    private readonly string _backupPath;

    public PatchedGlobalJsonFile(string source)
    {
        _filePath = source;
        _backupPath = $"{source}.{Guid.NewGuid().ToString()[0..8]}.tmp";
        File.Copy(_filePath, _backupPath, overwrite: true);
    }

    public static async Task<PatchedGlobalJsonFile?> TryPatchAsync(
        string path,
        NuGetVersion sdkVersion,
        CancellationToken cancellationToken)
    {
        var fileName = FileHelpers.FindFileInProject(path, WellKnownFileNames.GlobalJson);

        if (fileName != null)
        {
            var patched = new PatchedGlobalJsonFile(fileName);

            try
            {
                await patched.TryRemoveSdkVersionAsync(sdkVersion.ToString(), sdkVersion.IsPrerelease, cancellationToken);
                return patched;
            }
            catch (Exception)
            {
                patched.Dispose();
                throw;
            }
        }

        return null;
    }

    public async Task TryRemoveSdkVersionAsync(
        string sdkVersion,
        bool isPrerelease,
        CancellationToken cancellationToken)
    {
        if (!JsonHelpers.TryLoadObject(_filePath, out var globalJson))
        {
            return;
        }

        const string AllowPrereleaseProperty = "allowPrerelease";
        const string SdkProperty = "sdk";
        const string VersionProperty = "version";

        // Drop the version from the SDK property in the global.json file
        // but keep any other content, such as versions for MSBuild SDKs.
        if (globalJson.TryGetPropertyValue(SdkProperty, out var property) &&
            property?.GetValueKind() is JsonValueKind.Object)
        {
            var sdk = property.AsObject();

            var edited = false;

            if (!sdk.TryGetPropertyValue(VersionProperty, out var version) ||
                version?.GetValueKind() is JsonValueKind.String)
            {
                sdk.Remove(VersionProperty);
                sdk.Add(new(VersionProperty, JsonValue.Create(sdkVersion)));
                edited = true;
            }

            if (isPrerelease &&
                (!sdk.TryGetPropertyValue(AllowPrereleaseProperty, out var allowPrerelease) ||
                 allowPrerelease?.GetValueKind() is not JsonValueKind.True))
            {
                sdk.Remove(AllowPrereleaseProperty);
                sdk.Add(new(AllowPrereleaseProperty, JsonValue.Create(true)));
                edited = true;
            }

            if (edited)
            {
                await globalJson.SaveAsync(_filePath, cancellationToken);
            }
        }
    }

    public void Dispose()
        => File.Move(_backupPath, _filePath, overwrite: true);
}
