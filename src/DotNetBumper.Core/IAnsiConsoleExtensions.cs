// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Spectre.Console;

namespace MartinCostello.DotNetBumper;

/// <summary>
/// A class containing extension methods for the <see cref="IAnsiConsole"/> interface. This class cannot be inherited.
/// </summary>
public static class IAnsiConsoleExtensions
{
    /// <summary>
    /// Writes a disclaimer to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="channel">The version of .NET that the project was upgraded to.</param>
    public static void WriteDisclaimer(this IAnsiConsole console, Version channel)
    {
        string[] disclaimer =
        [
            $"{Emoji.Known.Megaphone} .NET Bumper upgrades are made on a best-effort basis.",
            $"{Emoji.Known.MagnifyingGlassTiltedRight} You should [bold]always[/] review the changes and test your project to validate the upgrade.",
            $"{Emoji.Known.OpenBook} [link={BreakingChangesLink(channel)}]Breaking changes in .NET {channel.Major}[/]",
        ];

        var panel = new Panel(string.Join(Environment.NewLine, disclaimer))
        {
            Border = BoxBorder.Rounded,
            Expand = true,
            Header = new PanelHeader("[yellow]Disclaimer[/]", Justify.Center),
        };

        console.Write(panel);

        static string BreakingChangesLink(Version channel)
            => $"https://learn.microsoft.com/dotnet/core/compatibility/{channel}";
    }

    /// <summary>
    /// Writes a message about use of a .NET runtime whose support period ends soon to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="upgrade">The <see cref="UpgradeInfo"/> that is nearing the end of support.</param>
    /// <param name="daysRemaining">The number of days remaining until support ends.</param>
    public static void WriteRuntimeNearingEndOfSupportWarning(this IAnsiConsole console, UpgradeInfo upgrade, double daysRemaining)
    {
        var eolUtc = upgrade.EndOfLife.GetValueOrDefault().ToDateTime(TimeOnly.MinValue);
        console.WriteWarningLine($"Support for .NET {upgrade.Channel} ends in {daysRemaining:N0} days on {eolUtc:D}.");
        console.WriteWarningLine("See [link=https://dotnet.microsoft.com/platform/support/policy/dotnet-core].NET and .NET Core Support Policy[/] for more information.");
    }

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The error message to write.</param>
    public static void WriteErrorLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[red]{Emoji.Known.CrossMark} {message}[/]");
    }

    /// <summary>
    /// Writes an exception message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The exception message to write.</param>
    /// <param name="exception">The exception to write.</param>
    public static void WriteExceptionLine(this IAnsiConsole console, string message, Exception exception)
    {
        console.WriteErrorLine(message);
        console.WriteException(exception);
    }

    /// <summary>
    /// Writes a progress message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The progress message to write.</param>
    public static void WriteProgressLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[grey]{message}[/]");
    }

    /// <summary>
    /// Writes as success message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The success message to write.</param>
    public static void WriteSuccessLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[green]{Emoji.Known.CheckMarkButton} {message}[/]");
    }

    /// <summary>
    /// Writes a message about use of an unsupported AWS Lambda runtime to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="upgrade">The <see cref="UpgradeInfo"/> that is not supported.</param>
    public static void WriteUnsupportedLambdaRuntimeWarning(this IAnsiConsole console, UpgradeInfo upgrade)
    {
        string qualifier = upgrade.ReleaseType is DotNetReleaseType.Lts ? "yet " : string.Empty;
        console.WriteWarningLine($".NET {upgrade.Channel} is not {qualifier}supported by AWS Lambda.");
    }

    /// <summary>
    /// Writes a warning message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The warning message to write.</param>
    public static void WriteWarningLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[yellow]{Emoji.Known.Warning} {message}[/]");
    }
}
