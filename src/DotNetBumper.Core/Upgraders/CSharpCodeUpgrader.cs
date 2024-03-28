// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.NetCore.CSharp.Analyzers.Performance;
using Spectre.Console;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed class CSharpCodeUpgrader(
    IAnsiConsole console,
    IEnvironment environment,
    IOptions<UpgradeOptions> options,
    DotNetProcess dotNet,
    ILogger<CSharpCodeUpgrader> logger) : FileUpgrader(console, environment, options, logger)
{
    public override int Order => int.MaxValue - 1;

    protected override string Action => "Upgrading C# code";

    protected override string InitialStatus => "Update C# code";

    protected override IReadOnlyList<string> Patterns => ["*.csproj"/*, "*.sln"*/];

    protected override async Task<ProcessingResult> UpgradeCoreAsync(
        UpgradeInfo upgrade,
        IReadOnlyList<string> fileNames,
        StatusContext context,
        CancellationToken cancellationToken)
    {
        string[] rules = ["CA1309"];

        var assembly = typeof(CSharpPreferIsEmptyOverCountFixer).Assembly;
        var providers = assembly.GetTypes()
            .Where((p) => p.IsSubclassOf(typeof(CodeFixProvider)))
            .Where((p) => !p.IsAbstract)
            .Where((p) => p.GetConstructor(Type.EmptyTypes) is not null)
            .Select((p) => (CodeFixProvider)Activator.CreateInstance(p)!)
            .Where((p) => p.FixableDiagnosticIds.Intersect(rules).Any())
            .OrderBy((p) => string.Join(',', p.FixableDiagnosticIds))
            .ToArray();

        // https://github.com/dotnet/upgrade-assistant/blob/main/src/components/Microsoft.DotNet.UpgradeAssistant.MSBuild/MSBuildWorkspaceUpgradeContext.cs
        // https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3
        // https://github.com/dotnet/upgrade-assistant/blob/d31f305d7ac5e7fbec37d1c3a8c2b8743058f8b6/samples/SourceUpdaterSample/MakeConstAnalyzer.cs
        MSBuildLocator.RegisterDefaults();

        ProcessingResult result = ProcessingResult.None;

        var properties = new Dictionary<string, string>()
        {
            ["AnalysisMode"] = "All",
            ["EnableNETAnalyzers"] = "true",
            ["EnforceCodeStyleInBuild"] = "true",
            ["TreatWarningsAsErrors"] = "false",
        };

        using (var workspace = MSBuildWorkspace.Create(properties))
        {
            foreach (var projectFile in fileNames)
            {
                var restoreResult = await dotNet.RunAsync(Path.GetDirectoryName(projectFile)!, ["restore"], new Dictionary<string, string?>(), cancellationToken);

                if (restoreResult != null)
                {
                    // TODO
                }

                var project = await workspace.OpenProjectAsync(projectFile, cancellationToken: cancellationToken);

                var compilation = await project.GetCompilationAsync(cancellationToken);

                if (compilation is not null)
                {
                    foreach (var diag in compilation.GetDiagnostics(cancellationToken).Where((p) => p.Severity is not DiagnosticSeverity.Hidden))
                    {
                        CodeAction? fixAction = null;
                        var document = project.GetDocument(diag.Location.SourceTree);

                        if (document is null)
                        {
                            continue;
                        }

                        var fixContext = new CodeFixContext(document, diag, (action, _) => fixAction = action, cancellationToken);

                        foreach (var provider in providers)
                        {
                            await provider.RegisterCodeFixesAsync(fixContext).ConfigureAwait(false);
                        }

                        if (fixAction is null)
                        {
                            continue;
                        }

                        var applyOperation = (await fixAction.GetOperationsAsync(cancellationToken))
                            .OfType<ApplyChangesOperation>()
                            .FirstOrDefault();

                        if (applyOperation is not null)
                        {
                            result = result.Max(applyOperation.ChangedSolution is null ? ProcessingResult.None : ProcessingResult.Success);
                        }
                    }
                }
            }
        }

        /*
        using (var workspace = MSBuildWorkspace.Create())
        {
            var solution = await workspace.OpenSolutionAsync("MySolution.sln", cancellationToken: cancellationToken);

            foreach (var project in solution.Projects)
            {
                var compilation = await project.GetCompilationAsync(cancellationToken);

                if (compilation is not null)
                {
                    foreach (var diag in compilation.GetDiagnostics(cancellationToken))
                    {
                    }
                }
            }
        }
        */

        return result;
    }
}
