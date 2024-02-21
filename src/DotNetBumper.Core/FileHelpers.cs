// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.DotNetBumper;

internal static class FileHelpers
{
    private static readonly Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static Stream OpenRead(string path, out FileMetadata metadata)
    {
        var stream = File.OpenRead(path);

        try
        {
            metadata = ReadMetadata(stream);
            return stream;
        }
        catch (Exception)
        {
            stream.Dispose();
            throw;
        }
    }

    public static Stream OpenWrite(string path, out FileMetadata metadata)
    {
        using (OpenRead(path, out metadata))
        {
        }

        return File.OpenWrite(path);
    }

    private static FileMetadata ReadMetadata(Stream stream)
    {
        Debug.Assert(stream.CanSeek, "The stream must be seekable.");
        Debug.Assert(stream.Position == 0, "The stream must be at the start.");

        // See https://stackoverflow.com/a/27976558/1064169
        using var reader = new StreamReader(stream, UTF8NoBom, leaveOpen: true);
        reader.Read();

        var encoding = reader.CurrentEncoding;

        stream.Seek(0, SeekOrigin.Begin);

        var newLine = GetNewLine(reader);

        stream.Seek(0, SeekOrigin.Begin);

        return new(encoding, newLine);
    }

    private static string GetNewLine(StreamReader reader)
    {
        bool hasCarriageReturn = false;

        while (reader.Peek() is not -1)
        {
            var ch = reader.Read();

            if (ch == '\n')
            {
                return hasCarriageReturn ? "\r\n" : "\n";
            }
            else if (hasCarriageReturn)
            {
                return "\r";
            }

            hasCarriageReturn = ch == '\r';
        }

        // Assume the current OS default
        return hasCarriageReturn ? "\r" : Environment.NewLine;
    }
}
