// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;

namespace MartinCostello.DotNetBumper;

internal static class ShouldlyExtensions
{
    public static void ShouldStartWithUTF8Bom(this byte[] actual)
        => AssertStartsWithBom(actual, true);

    public static void ShouldStartWithUTF8Bom(this ReadOnlySpan<byte> actual)
        => AssertStartsWithBom(actual, true);

    public static void ShouldNotStartWithUTF8Bom(this byte[] actual)
        => AssertStartsWithBom(actual, false);

    public static void ShouldNotStartWithUTF8Bom(this ReadOnlySpan<byte> actual)
        => AssertStartsWithBom(actual, false);

    private static void AssertStartsWithBom(this ReadOnlySpan<byte> actual, bool expected)
    {
        var bom = Encoding.UTF8.Preamble;

        actual.Length.ShouldBeGreaterThan(bom.Length);
        actual[0..bom.Length].SequenceEqual(bom).ShouldBe(expected);
    }
}
