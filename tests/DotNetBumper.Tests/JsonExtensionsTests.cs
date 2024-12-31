// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

namespace MartinCostello.DotNetBumper;

public static class JsonExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task File_Encoding_Is_Retained(bool writeBom)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = WriteJsonToFile(writeBom);
        JsonObject value = ReadJsonFromFile(path);

        // Act
        await value.SaveAsync(path, cancellationToken);

        // Assert
        byte[] contents = await File.ReadAllBytesAsync(path, cancellationToken);

        if (writeBom)
        {
            contents.ShouldStartWithUTF8Bom();
        }
        else
        {
            contents.ShouldNotStartWithUTF8Bom();
        }

        using var stream = File.OpenRead(path);

        var parsed = (await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken))!.AsObject();
        parsed["foo"]!.GetValue<string>().ShouldBe("bar");
    }

    private static JsonObject ReadJsonFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonNode.Parse(stream)!.AsObject();
    }

    private static string WriteJsonToFile(bool writeBom)
    {
        var bom = writeBom ? Encoding.UTF8.Preamble : [];
        var json = /*lang=json,strict*/ "{\"foo\":\"bar\"}"u8;

        string path = Path.GetTempFileName();

        using (var stream = File.OpenWrite(path))
        {
            stream.Write(bom);
            stream.Write(json);
        }

        return path;
    }
}
