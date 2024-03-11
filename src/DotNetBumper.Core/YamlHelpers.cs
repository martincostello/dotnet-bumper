// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper;

internal static class YamlHelpers
{
    public static YamlStream ParseFile(string fileName)
    {
        using var stream = File.OpenRead(fileName);
        using var reader = new StreamReader(stream);

        var yaml = new YamlStream();
        yaml.Load(reader);

        return yaml;
    }
}
