// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal sealed class TemporaryFile : IDisposable
{
    public string Path { get; } = System.IO.Path.GetTempFileName();

    public void Dispose()
    {
        try
        {
            File.Delete(Path);
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public bool Exists() => File.Exists(Path);
}
