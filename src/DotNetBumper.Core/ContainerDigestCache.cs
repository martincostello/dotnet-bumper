// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace MartinCostello.DotNetBumper;

internal sealed class ContainerDigestCache
{
    internal static readonly ContainerDigestCache Instance = new();

    public ConcurrentDictionary<string, string> ImageDigests { get; } = [];
}
