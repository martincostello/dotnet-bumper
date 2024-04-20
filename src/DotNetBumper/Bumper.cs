// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace MartinCostello.DotNetBumper;

[Command(
    Name = "dotnet bumper",
    FullName = "Upgrades projects to a newer version of .NET.")]
[HelpOption]
[VersionOptionFromMember(MemberName = nameof(GetVersion))]
internal partial class Bumper(ProjectUpgrader upgrader)
{
    [Argument(0, Description = "The path to directory containing a .NET 6+ project to be upgraded. If not specified, the current directory will be used.")]
    public string? ProjectPath { get; set; }

    [Option(
        "-cf|--configuration-file <PATH>",
        Description = "The path to a custom JSON or YAML configuration file to use, if any.")]
    public string? ConfigurationFile { get; set; }

    [Option(
        "-c|--channel <CHANNEL>",
        Description = "The .NET release channel to upgrade to in the format \"major.minor\".")]
    public string? DotNetChannel { get; set; }

    [Option(
        "-lf|--log-format <FORMAT>",
        Description = "The log format to use.")]
    public BumperLogFormat LogFormat { get; set; }

    [Option(
        "-lp|--log-path <PATH>",
        Description = "The path to write the log file to, if any.")]
    public string? LogPath { get; set; }

    [Option(
        "-q|--no-logo",
        Description = "Do not display the startup banner.")]
    public bool NoLogo { get; set; }

    [Option(
        "-t|--upgrade-type <TYPE>",
        Description = "The type of upgrade to perform.")]
    public UpgradeType? UpgradeType { get; set; }

    [Option(
        "-test|--test",
        Description = "Test the upgrade by running dotnet test on completion.")]
    public bool TestUpgrade { get; set; }

    [Option(
        "-timeout|--timeout <TIMESPAN>",
        Description = "The optional period to timeout the upgrade after.")]
    public TimeSpan? Timeout { get; set; }

    [Option(
        "-e|--warnings-as-errors",
        Description = "Treat any warnings encountered during the upgrade as errors.")]
    public bool WarningsAsErrors { get; set; }

    public static async Task<int> RunAsync(
        IAnsiConsole console,
        string[] args,
        Func<ILoggingBuilder, ILoggingBuilder> configureLogging,
        CancellationToken cancellationToken)
    {
        var logLevel = GetLogLevel(args);

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Bumper>()
            .Build();

        using var app = new CommandLineApplication<Bumper>();
        app.VerboseOption();

        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IModelAccessor>(app)
            .AddSingleton<IPostConfigureOptions<UpgradeOptions>, UpgradePostConfigureOptions>()
            .AddProjectUpgrader(configuration, console, (builder) => ConfigureLogging(configureLogging(builder), logLevel))
            .BuildServiceProvider();

        app.Conventions
            .UseDefaultConventions()
            .UseConstructorInjection(serviceProvider);

        try
        {
            return await app.ExecuteAsync(args, cancellationToken);
        }
        catch (CommandParsingException ex)
        {
            console.WriteLine();
            console.WriteWarningLine(ex.Message);
            return 1;
        }

        static ILoggingBuilder ConfigureLogging(ILoggingBuilder builder, LogLevel logLevel)
            => builder.SetMinimumLevel(logLevel)
                      .AddFilter("Microsoft.Extensions.Http.DefaultHttpClientFactory", (p) => p > LogLevel.Debug);
    }

    public static string GetVersion() => ProjectUpgrader.Version;

    public async Task<int> OnExecute(
        IAnsiConsole console,
        ILogger<Bumper> logger,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutSource = null;

        try
        {
            if (!NoLogo)
            {
                console.Write(new FigletText(".NET Bumper").Color(Color.Purple));
                console.MarkupLineInterpolated($"[{Color.Blue}]v{GetVersion()} (.NET v{Environment.Version})[/]");
                console.WriteLine();
            }

            var stopwatch = Stopwatch.StartNew();

            int status;

            if (Timeout is { } timeout)
            {
                timeoutSource = new CancellationTokenSource(timeout);
            }

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource?.Token ?? CancellationToken.None))
            {
                status = await upgrader.UpgradeAsync(cts.Token);
            }

            stopwatch.Stop();

            console.WriteLine();
            console.MarkupLine($"Duration: [green]{stopwatch.Elapsed}.[/]");

            return status;
        }
        catch (Exception ex)
        {
            Log.UpgradeFailed(logger, ex);

            if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                console.WriteLine();
                console.WriteWarningLine("Upgrade cancelled by user.");
                return 2;
            }
            else if (ex is OperationCanceledException && timeoutSource?.Token.IsCancellationRequested is true)
            {
                console.WriteLine();
                console.WriteErrorLine($"Upgrade timed out after {Timeout!.Value}.");
                return 3;
            }
            else if (ex is OptionsValidationException)
            {
                console.WriteLine();
                console.WriteWarningLine(ex.Message);
            }
            else
            {
                console.WriteLine();
                console.WriteExceptionLine("Failed to upgrade project.", ex);
            }

            return 1;
        }
        finally
        {
            timeoutSource?.Dispose();
        }
    }

    private static LogLevel GetLogLevel(string[] args)
    {
        bool verbose =
            args.Contains("-v", StringComparer.OrdinalIgnoreCase) ||
            args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);

        return verbose ? LogLevel.Debug : LogLevel.None;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Error,
            Message = "Failed to upgrade project.")]
        public static partial void UpgradeFailed(ILogger logger, Exception exception);
    }
}
