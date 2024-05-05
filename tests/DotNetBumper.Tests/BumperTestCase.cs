// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public sealed class BumperTestCase(
    string sdkVersion,
    IList<string>? targetFrameworks = null,
    IList<string>? args = null,
    IDictionary<string, string>? packageReferences = null) : IXunitSerializable
{
    private Version? _channel;

    public BumperTestCase()
        : this(string.Empty)
    {
    }

    public string SdkVersion { get; set; } = sdkVersion;

    public IList<string> TargetFrameworks { get; set; } = targetFrameworks ?? [];

    public IDictionary<string, string> PackageReferences { get; set; } = packageReferences ?? new Dictionary<string, string>(0);

    public IList<string> Arguments { get; set; } = args ?? [];

    public Version Channel
    {
        get
        {
            if (_channel is null)
            {
                var sdkVersion = Version.Parse(SdkVersion);
                _channel = new(sdkVersion.Major, sdkVersion.Minor);
            }

            return _channel;
        }
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        Arguments = info.GetValue<string[]>(nameof(Arguments));
        SdkVersion = info.GetValue<string>(nameof(SdkVersion));
        TargetFrameworks = info.GetValue<string[]>(nameof(TargetFrameworks));

        if (info.GetValue<string>(nameof(Channel)) is { Length: > 0 } channel)
        {
            _channel = new Version(channel);
        }

        if (info.GetValue<string[]>(nameof(PackageReferences)) is { Length: > 0 } references)
        {
            PackageReferences = new Dictionary<string, string>(references.Length);

            for (int i = 0; i < references.Length; i++)
            {
                if (references[i].Split('=') is [var package, var version])
                {
                    PackageReferences[package] = version;
                }
            }
        }
    }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Arguments), Arguments.ToArray());
        info.AddValue(nameof(Channel), _channel?.ToString());
        info.AddValue(nameof(SdkVersion), SdkVersion);
        info.AddValue(nameof(TargetFrameworks), TargetFrameworks.ToArray());

        if (PackageReferences is { Count: > 0 })
        {
            info.AddValue(nameof(PackageReferences), PackageReferences.Select((p) => $"{p.Key}={p.Value}").ToArray());
        }
    }

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
