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
    /// <param name="environment">The current <see cref="IEnvironment"/>.</param>
    /// <param name="channel">The version of .NET that the project was upgraded to.</param>
    public static void WriteDisclaimer(
        this IAnsiConsole console,
        IEnvironment environment,
        Version channel)
    {
        List<string> disclaimer =
        [
            $"{Emoji.Known.Megaphone} .NET Bumper upgrades are made on a best-effort basis.",
            $"{Emoji.Known.MagnifyingGlassTiltedRight} You should [bold]always[/] review the changes and test your project to validate the upgrade.",
        ];

        var breakingChangesEmoji = Emoji.Known.OpenBook;
        var breakingChangesTitle = $"Breaking changes in .NET {channel.Major}";
        var breakingChangesUrl = BreakingChangesUrl(channel);

        if (environment.SupportsLinks)
        {
            disclaimer.Add($"{breakingChangesEmoji} [link={breakingChangesUrl}]{breakingChangesTitle}[/]");
        }
        else
        {
            disclaimer.Add($"{breakingChangesEmoji} [bold]{breakingChangesTitle}[/] - {breakingChangesUrl}");
        }

        var panel = new Panel(string.Join(Environment.NewLine, disclaimer))
        {
            Border = BoxBorder.Rounded,
            Expand = true,
            Header = new PanelHeader($"[{Color.Yellow}]Disclaimer[/]", Justify.Center),
        };

        console.Write(panel);

        static string BreakingChangesUrl(Version channel)
            => $"https://learn.microsoft.com/dotnet/core/compatibility/{channel}";
    }

    /// <summary>
    /// Writes a message about use of a .NET runtime whose support period ends soon to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="environment">The current <see cref="IEnvironment"/>.</param>
    /// <param name="upgrade">The <see cref="UpgradeInfo"/> that is nearing the end of support.</param>
    /// <param name="daysRemaining">The number of days remaining until support ends.</param>
    public static void WriteRuntimeNearingEndOfSupportWarning(
        this IAnsiConsole console,
        IEnvironment environment,
        UpgradeInfo upgrade,
        double daysRemaining)
    {
        var eolUtc = upgrade.EndOfLife.GetValueOrDefault().ToDateTime(TimeOnly.MinValue);
        console.WriteWarningLine($"Support for .NET {upgrade.Channel} ends in {daysRemaining:N0} days on {eolUtc:D}.");

        var supportPolicyTitle = ".NET and .NET Core Support Policy";
        var supportPolicyUrl = SupportPolicyUrl(upgrade.Channel);

        if (environment.SupportsLinks)
        {
            console.WriteWarningLine($"See [link={supportPolicyUrl}]{supportPolicyTitle}[/] for more information.");
        }
        else
        {
            console.WriteWarningLine($"See [bold]{supportPolicyTitle}[/] for more information: {supportPolicyUrl}");
        }

        static string SupportPolicyUrl(Version channel)
            => $"https://learn.microsoft.com/dotnet/core/compatibility/{channel}";
    }

    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The error message to write.</param>
    public static void WriteErrorLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[{Color.Red}]{Emoji.Known.CrossMark} {message}[/]");
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
        console.MarkupLineInterpolated($"[{Color.Grey}]{message}[/]");
    }

    /// <summary>
    /// Writes as success message to the console.
    /// </summary>
    /// <param name="console">The <see cref="IAnsiConsole"/> to use.</param>
    /// <param name="message">The success message to write.</param>
    public static void WriteSuccessLine(this IAnsiConsole console, string message)
    {
        console.MarkupLineInterpolated($"[{Color.Green}]{Emoji.Known.CheckMarkButton} {message}[/]");
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
        console.MarkupLineInterpolated($"[{Color.Yellow}]{Emoji.Known.Warning} {message}[/]");
    }
}
