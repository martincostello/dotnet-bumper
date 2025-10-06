// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

[CollectionDefinition(nameof(NotInParallelCollection), DisableParallelization = true)]
public sealed class NotInParallelCollection;
