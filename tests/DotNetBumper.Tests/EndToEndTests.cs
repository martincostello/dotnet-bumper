// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public static class EndToEndTests
{
    [Fact]
    public static async Task Application_Does_Not_Error()
    {
        // Act and Assert
        await Should.NotThrowAsync(Program.Main);
    }
}
