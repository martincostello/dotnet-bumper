// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;

namespace MartinCostello.DotNetBumper;

public sealed class BumperTestCase(
    string sdkVersion,
    IList<string>? targetFrameworks = null,
    IList<string>? args = null,
    IDictionary<string, string>? packageReferences = null)
{
    public string SdkVersion { get; } = sdkVersion;

    public IList<string> TargetFrameworks { get; } = targetFrameworks ?? [];

    public IDictionary<string, string> PackageReferences { get; } = packageReferences ?? new Dictionary<string, string>(0);

    public IList<string> Arguments { get; } = args ?? [];

    public override string ToString()
    {
        var builder = new StringBuilder().Append(CultureInfo.InvariantCulture, $"SDK: {SdkVersion}");

        if (TargetFrameworks is { Count: > 0 } tfms)
        {
            builder.Append(CultureInfo.InvariantCulture, $"; TFM{(tfms.Count is 1 ? string.Empty : "s")}: [")
                   .Append(string.Join(", ", tfms))
                   .Append(']');
        }

        if (PackageReferences is { Count: > 0 } packages)
        {
            builder.Append("; Packages: [")
                   .Append(string.Join(", ", packages.Keys))
                   .Append(']');
        }

        if (Arguments is { Count: > 0 } args)
        {
            builder.Append("; Args: [")
                   .Append(string.Join(", ", args))
                   .Append(']');
        }

        return builder.ToString();
    }
}
