// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Logging;

internal static class ILoggerExtensions
{
    public static string GetMSBuildVerbosity(this ILogger logger)
        => logger.IsEnabled(LogLevel.Debug) ?
#if DEBUG
            "detailed" :
#else
            "detailed" :
#endif
            "minimal";
}
