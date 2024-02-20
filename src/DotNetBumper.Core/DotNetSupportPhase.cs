// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

/// <summary>
/// An enumeration representing a type of .NET support phases.
/// </summary>
/// <remarks>
/// See https://json.schemastore.org/dotnet-releases-index.json.
/// </remarks>
public enum DotNetSupportPhase
{
    /// <summary>
    /// The product is in preview.
    /// </summary>
    Preview = 0,

    /// <summary>
    /// The product is in a go-live phase.
    /// </summary>
    GoLive,

    /// <summary>
    /// The product is being actively developed.
    /// </summary>
    Active,

    /// <summary>
    /// The product is in a maintenance phase.
    /// </summary>
    Maintenance,

    /// <summary>
    /// The product has reached end-of-life.
    /// </summary>
    Eol,
}
