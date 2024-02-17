// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MartinCostello.DotNetBumper.Upgrades;

internal sealed partial class TargetFrameworkUpgrader(
    IOptions<UpgradeOptions> options,
    ILogger<GlobalJsonUpgrader> logger) : IUpgrader
{
    public async Task<bool> UpgradeAsync(
        UpgradeInfo upgrade,
        CancellationToken cancellationToken)
    {
        Log.UpgradingTargetFramework(logger);

        string projectPath = options.Value.ProjectPath;

        bool filesChanged = false;

        string[] files =
        [
            ..Directory.GetFiles(projectPath, "*.csproj", SearchOption.AllDirectories),
            ..Directory.GetFiles(projectPath, "*.fsproj", SearchOption.AllDirectories),
        ];

        foreach (var filePath in files)
        {
            XDocument project;

            using (var stream = File.OpenRead(filePath))
            {
                project = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, cancellationToken);
            }

            var tfm = project.Root?.Element("PropertyGroup")?.Element("TargetFramework");

            if (tfm is not null)
            {
                // TODO Update the TFM
            }
        }

        return filesChanged;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
           EventId = 1,
           Level = LogLevel.Debug,
           Message = "Upgrading target framework moniker.")]
        public static partial void UpgradingTargetFramework(ILogger logger);
    }
}
