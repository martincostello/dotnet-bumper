// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using NSubstitute;

namespace MartinCostello.DotNetBumper;

public class BumperTestLoggerTests
{
    [Fact]
    public void BumperTestLoggerTests_Initialize_Throws_If_No_Path_Specified()
    {
        // Arrange
        var events = Substitute.For<TestLoggerEvents>();
        var testRunDirectory = string.Empty;

        var logger = new BumperTestLogger();

        // Act and Assert
        Should.Throw<InvalidOperationException>(
            () => logger.Initialize(events, testRunDirectory));
    }
}
