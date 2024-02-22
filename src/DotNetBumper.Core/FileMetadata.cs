// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// Represents metadata for a file that may edited.
/// </summary>
/// <param name="Encoding">The file's encoding.</param>
/// <param name="NewLine">The file's new line delimiter.</param>
internal sealed record class FileMetadata(
    Encoding Encoding,
    string NewLine);
