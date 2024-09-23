// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Management.Automation.Language;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper;

internal sealed class PowerShellScript
{
    private static readonly string[] _otherScriptExtensions = [".bash", ".cmd", ".sh"];

    private PowerShellScript(
        FileMetadata fileMetadata,
        List<string> lines,
        List<(int LineOffset, int ColumnOffset, Range Location, Ast SyntaxTree)> syntaxTrees)
    {
        FileMetadata = fileMetadata;
        Lines = lines;
        SyntaxTrees = syntaxTrees;
    }

    public FileMetadata FileMetadata { get; }

    public List<string> Lines { get; }

    private List<(int LineOffset, int ColumnOffset, Range Location, Ast SyntaxTree)> SyntaxTrees { get; }

    public static async Task<PowerShellScript?> TryParseAsync(
        YamlStream workflow,
        string path,
        CancellationToken cancellationToken)
    {
        (var lines, var metadata) = await ReadScriptAsync(path, cancellationToken);

        var finder = new PowerShellRunStepFinder();
        workflow.Accept(finder);

        if (finder.ScriptLocations.Count is 0)
        {
            return null;
        }

        List<(int LineOffset, int ColumnOffset, Range Location, Ast SyntaxTree)> syntaxTrees = [];

        string contents = string.Join(metadata.NewLine, lines) + metadata.NewLine;

        foreach ((var lineOffset, var columnOffset, var range) in finder.ScriptLocations)
        {
            string script = contents[range];
            var location = range;

            if (script[0] is '>' or '|')
            {
                location = new(new(location.Start.Value + 1), location.End);
                script = script[1..];
            }

            // We are more lenient to errors in PowerShell scripts embedded in GitHub Actions workflows for two reasons:
            // 1. The run script might not explicitly specify a shell, in which case it might be parseable as PowerShell
            //    event if it is not explicitly written as it. For example: `run: dotnet build -c Release -f net6.0`.
            // 2. A script might be explicitly marked as PowerShell, but use GitHub Actions workflow syntax that causes
            //    syntax errors when parsing before the templates have been substituted from the snippet. For example:
            //    shell: pwsh
            //    run: dotnet build -c "${{ env.BUILD_CONFIGURATION }}" -f net6.0
            if (ParseScript(script, path, allowErrors: true) is { } syntaxTree)
            {
                syntaxTrees.Add((lineOffset, columnOffset, location, syntaxTree));
            }
        }

        return new(metadata, lines, syntaxTrees);
    }

    public static async Task<PowerShellScript?> TryParseAsync(string path, CancellationToken cancellationToken)
    {
        (var lines, var metadata) = await ReadScriptAsync(path, cancellationToken);

        string contents = string.Join(metadata.NewLine, lines);

        // Attempt to treat other scripts as PowerShell, as the syntax may be similar
        // enough that we can still upgrade them anyway for simple command invocations.
        var allowErrors = _otherScriptExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        var syntaxTree = ParseScript(contents, path, allowErrors);

        if (syntaxTree is null)
        {
            return null;
        }

        var location = new Range(0, Math.Max(0, contents.Length - 1));

        return new(metadata, lines, [(0, 0, location, syntaxTree)]);
    }

    public bool TryUpdate(Version channel)
    {
        bool edited = false;
        var builder = new StringBuilder();

        foreach ((var lineOffset, var columnOffset, var range, var syntaxTree) in SyntaxTrees)
        {
            var visitor = new SyntaxTreeVisitor(channel);
            syntaxTree.Visit(visitor);

            if (visitor.Edits.Count is 0)
            {
                continue;
            }

            foreach (var edits in visitor.Edits.GroupBy((p) => p.Location.StartLineNumber))
            {
                int offset = 0;
                int lineIndex = lineOffset + edits.Key - 1;

                var original = Lines[lineIndex].AsSpan();
                builder.Clear();

                foreach ((var location, var replacement) in edits.OrderBy((p) => p.Location.StartOffset))
                {
                    int start = columnOffset + location.StartColumnNumber - 1;
                    int end = columnOffset + location.EndColumnNumber - 1;

                    builder.Append(original[offset..start])
                           .Append(replacement);

                    offset = end;
                }

                builder.Append(original[offset..]);

                Lines[lineIndex] = builder.ToString();
            }

            edited |= true;
        }

        return edited;
    }

    private static async Task<(List<string> Lines, FileMetadata Metadata)> ReadScriptAsync(string path, CancellationToken cancellationToken)
    {
        using var input = FileHelpers.OpenRead(path, out var metadata);
        using var reader = new StreamReader(input, metadata.Encoding);

        List<string> lines = [];
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lines.Add(line);
        }

        return (lines, metadata);
    }

    private static ScriptBlockAst? ParseScript(string input, string fileName, bool allowErrors = false)
    {
        try
        {
            var syntaxTree = Parser.ParseInput(input, fileName, out _, out var errors);
            return allowErrors || errors.Length is 0 ? syntaxTree : null;
        }
        catch (Exception) when (allowErrors)
        {
            // We tried to parse something as PowerShell, and we failed miserably,
            // the lesson is: ~~never try~~ at least we tried.
            return null;
        }
    }

    private sealed class PowerShellRunStepFinder() : YamlVisitorBase
    {
        public IList<(int LineIndex, int ColumnIndex, Range Range)> ScriptLocations { get; } = [];

        protected override void VisitPair(YamlNode key, YamlNode value)
        {
            if (key is YamlScalarNode { Value: "steps" } &&
                value is YamlSequenceNode { Children.Count: > 0 } items)
            {
                foreach (var item in items)
                {
                    if (item is not YamlMappingNode { Children.Count: > 0 } step)
                    {
                        continue;
                    }

                    var run = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "run" }).Value;

                    if (run != default)
                    {
                        var shell = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "shell" }).Value;

                        if (shell != default)
                        {
                            // See https://docs.github.com/actions/writing-workflows/workflow-syntax-for-github-actions#jobsjob_idstepsshell
                            if (shell is not YamlScalarNode { Value: "pwsh" or "powershell" or "bash" or "sh" or "cmd" })
                            {
                                continue;
                            }
                        }

                        int columnOffset = (int)(run.Start.Line == run.End.Line ? run.Start.Column - 1 : 0);
                        ScriptLocations.Add(((int)run.Start.Line - 1, columnOffset, new((int)run.Start.Index, (int)run.End.Index)));
                    }
                }
            }

            base.VisitPair(key, value);
        }
    }

    private sealed class SyntaxTreeVisitor(Version channel) : AstVisitor2
    {
        public List<(IScriptExtent Location, string Replacement)> Edits { get; } = [];

        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            foreach (var element in commandAst.CommandElements)
            {
                if (element is StringConstantExpressionAst constant)
                {
                    TryUpdateConstant(constant);
                }
            }

            return base.VisitCommand(commandAst);
        }

        public override AstVisitAction VisitParameter(ParameterAst parameterAst)
        {
            if (parameterAst.DefaultValue is StringConstantExpressionAst constant)
            {
                TryUpdateConstant(constant);
            }

            return base.VisitParameter(parameterAst);
        }

        public override AstVisitAction VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            if (assignmentStatementAst.Right is CommandExpressionAst command &&
                command.Expression is StringConstantExpressionAst constant)
            {
                TryUpdateConstant(constant);
            }

            return base.VisitAssignmentStatement(assignmentStatementAst);
        }

        private void TryUpdateConstant(StringConstantExpressionAst constant)
        {
            if (TargetFrameworkHelpers.TryUpdateTfm(constant.Value, channel, out var updated) ||
                RuntimeIdentifierHelpers.TryUpdateRid(constant.Value, out updated))
            {
                char? quote = constant.StringConstantType switch
                {
                    StringConstantType.SingleQuoted or StringConstantType.SingleQuotedHereString => '\'',
                    StringConstantType.DoubleQuoted or StringConstantType.DoubleQuotedHereString => '"',
                    _ => null,
                };

                if (quote is { } value)
                {
                    updated = $"{value}{updated}{value}";
                }

                Edits.Add((constant.Extent, updated));
            }
        }
    }
}
