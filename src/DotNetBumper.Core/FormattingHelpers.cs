// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MartinCostello.DotNetBumper;

internal static class FormattingHelpers
{
    private static readonly Encoding UTF8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static Encoding DetectEncoding(Stream stream)
    {
        Debug.Assert(stream.CanSeek, "The stream must be seekable.");
        Debug.Assert(stream.Position == 0, "The stream must be at the start.");

        // See https://stackoverflow.com/a/27976558/1064169
        using var reader = new StreamReader(stream, UTF8NoBom, leaveOpen: true);
        reader.Read();

        var encoding = reader.CurrentEncoding;

        stream.Seek(0, SeekOrigin.Begin);

        return encoding;
    }

    public static Stream OpenFileWithEncoding(string path, out Encoding encoding)
    {
        var stream = File.OpenRead(path);

        try
        {
            encoding = FormattingHelpers.DetectEncoding(stream);
            return stream;
        }
        catch (Exception)
        {
            stream.Dispose();
            throw;
        }
    }
}
