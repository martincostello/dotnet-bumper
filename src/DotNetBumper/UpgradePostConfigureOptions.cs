﻿// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using McMaster.Extensions.CommandLineUtils.Abstractions;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper;

internal sealed class UpgradePostConfigureOptions(IModelAccessor accessor) : IPostConfigureOptions<UpgradeOptions>
{
    public void PostConfigure(string? name, UpgradeOptions options)
    {
        var command = (Bumper)accessor.GetModel();

        options.DotNetChannel ??= command.DotNetChannel;
        options.LogFormat = command.LogFormat;
        options.TestUpgrade = command.TestUpgrade;
        options.TreatWarningsAsErrors = command.WarningsAsErrors;

        if (command.ConfigurationFile is not null)
        {
            options.ConfigurationPath = Path.GetFullPath(command.ConfigurationFile);
        }

        if (!string.IsNullOrWhiteSpace(command.ProjectPath))
        {
            options.ProjectPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(command.ProjectPath));
        }

        if (command.Quality is { } quality)
        {
            options.Quality = quality;
        }

        if (command.UpgradeType is { } upgradeType)
        {
            options.UpgradeType = upgradeType;
        }

        if (command.LogPath is not null)
        {
            options.LogPath = Path.GetFullPath(command.LogPath);
        }
    }
}
