// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper;

public static class JsonExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async void File_Encoding_Is_Retained(bool writeBom)
    {
        // Arrange
        var path = WriteJsonToFile(writeBom);
        JsonObject value = ReadJsonFromFile(path);

        // Act
        await value.SaveAsync(path, CancellationToken.None);

        // Assert
        byte[] contents = await File.ReadAllBytesAsync(path);

        AssertStartsWithBom(contents, writeBom);

        using var stream = File.OpenRead(path);

        var parsed = JsonNode.Parse(stream)!.AsObject();
        parsed["foo"]!.GetValue<string>().ShouldBe("bar");
    }

    private static void AssertStartsWithBom(ReadOnlySpan<byte> value, bool expected)
    {
        var bom = Encoding.UTF8.Preamble;
        var actual = value[0..3];
        actual.SequenceEqual(bom).ShouldBe(expected);
    }

    private static JsonObject ReadJsonFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonNode.Parse(stream)!.AsObject();
    }

    private static string WriteJsonToFile(bool writeBom)
    {
        var bom = writeBom ? Encoding.UTF8.Preamble : [];
        var json = "{\"foo\":\"bar\"}"u8;

        string path = Path.GetTempFileName();

        using (var stream = File.OpenWrite(path))
        {
            stream.Write(bom);
            stream.Write(json);
        }

        return path;
    }
}
