// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace MartinCostello.DotNetBumper;

internal static class VersionExtensions
{
    public static string ToLambdaRuntime(this Version version)
        => $"dotnet{version.ToString(1)}";

    public static string ToTargetFramework(this Version version)
        => $"net{version.ToString(2)}";

    public static string ToTargetFramework(this NuGetVersion version)
        => $"net{version.Major}.{version.Minor}";

    public static Version? ToVersion(this string targetFramework)
    {
        if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            int digit = targetFramework.AsSpan().IndexOfAnyInRange('1', '9');

            if (digit is not -1 && Version.TryParse(targetFramework[digit..], out var version))
            {
                return new(version.Major, version.Minor);
            }
        }

        return null;
    }
}
