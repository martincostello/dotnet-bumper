// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.PostProcessors;

/// <summary>
/// An enumeration of the test platforms that a project's tests can be run with.
/// </summary>
internal enum TestPlatform
{
    /// <summary>
    /// VSTest.
    /// </summary>
    VSTest = 0,

    /// <summary>
    /// Microsoft Testing Platform (MTP).
    /// </summary>
    MicrosoftTestingPlatform,
}
