// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

internal static class ProcessingResultExtensions
{
    public static ProcessingResult Max(this ProcessingResult value, ProcessingResult other)
        => (ProcessingResult)Math.Max((int)value, (int)other);
}
