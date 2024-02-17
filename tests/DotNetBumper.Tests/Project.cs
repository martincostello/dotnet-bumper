// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal sealed class Project : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("dotnet-bumper");

    public string DirectoryName => _directory.FullName;

    public async Task AddFileAsync(string path, string content)
        => await File.WriteAllTextAsync(GetFilePath(path), content);

    public string GetFilePath(string path) => Path.Combine(DirectoryName, path);

    public async Task<string> GetFileAsync(string path)
        => await File.ReadAllTextAsync(GetFilePath(path));

    public void Dispose()
    {
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}
