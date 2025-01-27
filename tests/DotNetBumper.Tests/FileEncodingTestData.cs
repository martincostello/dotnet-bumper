﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public sealed class FileEncodingTestData : TheoryData<string, bool>
{
    public FileEncodingTestData()
    {
        Add("\n", false);
        Add("\n", true);
        Add("\r\n", false);
        Add("\r\n", true);
    }
}
