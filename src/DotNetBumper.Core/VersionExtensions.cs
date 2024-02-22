// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace MartinCostello.DotNetBumper;

internal static partial class VersionExtensions
{
    private const string TfmPattern = "net(coreapp)?[1-9]+\\.[0-9]{1}";

    public static bool IsTargetFrameworkMoniker(this string value)
        => TargetFrameworkMoniker().IsMatch(value);

    public static bool IsTargetFrameworkMoniker(this ReadOnlySpan<char> value)
        => TargetFrameworkMoniker().IsMatch(value);

    public static MatchCollection MatchTargetFrameworkMonikers(this string value)
        => ContainsTargetFrameworkMoniker().Matches(value);

    public static string ToLambdaRuntime(this Version version)
        => $"dotnet{version.ToString(1)}";

    public static string ToTargetFramework(this Version version)
        => $"net{version.ToString(2)}";

    public static Version? ToVersionFromLambdaRuntime(this string runtime)
        => runtime.AsSpan().ToVersionFromLambdaRuntime();

    public static Version? ToVersionFromLambdaRuntime(this ReadOnlySpan<char> runtime)
    {
        if (runtime.StartsWith("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            int digit = runtime.IndexOfAnyInRange('1', '9');

            if (digit is not -1)
            {
                var number = runtime[digit..];

                if (number.IndexOf('.') is -1)
                {
                    number = $"{number}.0";
                }

                if (Version.TryParse(number, out var version))
                {
                    return version;
                }
            }
        }

        return null;
    }

    public static Version? ToVersionFromTargetFramework(this string targetFramework)
        => targetFramework.AsSpan().ToVersionFromTargetFramework();

    public static Version? ToVersionFromTargetFramework(this ReadOnlySpan<char> targetFramework)
    {
        if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase))
        {
            int digit = targetFramework.IndexOfAnyInRange('1', '9');

            if (digit is not -1 && Version.TryParse(targetFramework[digit..], out var version))
            {
                return new(version.Major, version.Minor);
            }
        }

        return null;
    }

    [GeneratedRegex($"(?<!dot){TfmPattern}")]
    private static partial Regex ContainsTargetFrameworkMoniker();

    [GeneratedRegex($"^{TfmPattern}$")]
    private static partial Regex TargetFrameworkMoniker();
}
