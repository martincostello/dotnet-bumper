// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class that isolates the test process from the environment it is launched in. This class cannot be inherited.
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable(WellKnownEnvironmentVariables.DirectoryBuildPropertiesPath, null);
}
