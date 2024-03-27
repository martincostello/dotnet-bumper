// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.NetCore.CSharp.Analyzers.Performance;

namespace MartinCostello.DotNetBumper.Upgraders;

internal sealed class CSharpCodeUpgrader : IUpgrader
{
    public int Order => int.MaxValue - 1;

    public async Task<ProcessingResult> UpgradeAsync(UpgradeInfo upgrade, CancellationToken cancellationToken)
    {
        CodeFixProvider[] providers = [new CSharpPreferIsEmptyOverCountFixer()];

        // https://github.com/dotnet/upgrade-assistant/blob/main/src/components/Microsoft.DotNet.UpgradeAssistant.MSBuild/MSBuildWorkspaceUpgradeContext.cs
        // https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3
        // https://github.com/dotnet/upgrade-assistant/blob/d31f305d7ac5e7fbec37d1c3a8c2b8743058f8b6/samples/SourceUpdaterSample/MakeConstAnalyzer.cs
        MSBuildLocator.RegisterDefaults();

        ProcessingResult result = ProcessingResult.None;

        using (var workspace = MSBuildWorkspace.Create())
        {
            var project = await workspace.OpenProjectAsync("MyProject.csproj", cancellationToken: cancellationToken);
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is not null)
            {
                foreach (var diag in compilation.GetDiagnostics(cancellationToken))
                {
                    CodeAction? fixAction = null;
                    var document = project.GetDocument(diag.Location.SourceTree);

                    if (document is null)
                    {
                        continue;
                    }

                    var context = new CodeFixContext(document, diag, (action, _) => fixAction = action, cancellationToken);

                    foreach (var provider in providers)
                    {
                        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
                    }

                    var applyOperation = (await fixAction!.GetOperationsAsync(cancellationToken))
                        .OfType<ApplyChangesOperation>()
                        .FirstOrDefault();

                    if (applyOperation is not null)
                    {
                        result = result.Max(applyOperation.ChangedSolution is null ? ProcessingResult.None : ProcessingResult.Success);
                    }
                }
            }
        }

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

        return result;
    }
}
