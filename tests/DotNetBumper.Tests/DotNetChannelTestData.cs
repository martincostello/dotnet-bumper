// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public sealed class DotNetChannelTestData : TheoryData<string>
{
    public DotNetChannelTestData()
    {
        const int MinimumVersion = 8;

        foreach (int version in Enumerable.Range(MinimumVersion, 3))
        {
            Add(version.ToString("N1", CultureInfo.InvariantCulture));
        }
    }
}
