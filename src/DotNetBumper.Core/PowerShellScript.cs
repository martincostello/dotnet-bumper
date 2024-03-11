// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Management.Automation.Language;
using MartinCostello.DotNetBumper.Upgraders;
using YamlDotNet.RepresentationModel;

namespace MartinCostello.DotNetBumper;

internal sealed class PowerShellScript
{
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
        using var input = FileHelpers.OpenRead(path, out var metadata);
        using var reader = new StreamReader(input, metadata.Encoding);

        List<string> lines = [];
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lines.Add(line);
        }

        var finder = new PowerShellRunStepFinder();
        workflow.Accept(finder);

        if (finder.ScriptLocations.Count is 0)
        {
            return null;
        }

        List<(int LineOffset, int ColumnOffset, Range Location, Ast SyntaxTree)> syntaxTrees = [];

        string allContents = string.Join(metadata.NewLine, lines);

        foreach ((var lineOffset, var columnOffset, var location) in finder.ScriptLocations)
        {
            string contents = allContents[location];
            var scriptLocation = location;

            if (contents[0] is '>' or '|')
            {
                scriptLocation = new(new(scriptLocation.Start.Value + 1), scriptLocation.End);
                contents = contents[1..];
            }
            else
            {
                scriptLocation = new(new(scriptLocation.Start.Value), scriptLocation.End);
            }

            var syntaxTree = Parser.ParseInput(contents, path, out _, out var errors);

            if (errors.Length is 0)
            {
                syntaxTrees.Add((lineOffset, columnOffset, location, syntaxTree));
            }
        }

        return new PowerShellScript(metadata, lines, syntaxTrees);
    }

    public static async Task<PowerShellScript?> TryParseAsync(string path, CancellationToken cancellationToken)
    {
        using var input = FileHelpers.OpenRead(path, out var metadata);
        using var reader = new StreamReader(input, metadata.Encoding);

        List<string> lines = [];
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lines.Add(line);
        }

        string contents = string.Join(metadata.NewLine, lines);

        var syntaxTree = Parser.ParseInput(contents, path, out _, out var errors);

        if (errors.Length is not 0)
        {
            return null;
        }

        var location = new Range(0, Math.Max(0, contents.Length - 1));

        return new PowerShellScript(metadata, lines, [(0, 0, location, syntaxTree)]);
    }

    public bool TryUpdate(Version channel)
    {
        bool edited = false;

        foreach ((var lineOffset, var columnOffset, var range, var syntaxTree) in SyntaxTrees)
        {
            var vistor = new SyntaxTreeVisitor(channel);
            syntaxTree.Visit(vistor);

            if (vistor.Edits.Count is 0)
            {
                continue;
            }

            foreach (var edits in vistor.Edits.GroupBy((p) => p.Location.StartLineNumber))
            {
                int offset = 0;
                int lineIndex = lineOffset + edits.Key - 1;

                var original = Lines[lineIndex];
                var builder = new StringBuilder();

                foreach ((var location, var replacement) in edits.OrderBy((p) => p.Location.StartOffset))
                {
                    int start = location.StartColumnNumber - 1 + columnOffset;
                    int end = location.EndColumnNumber - 1 + columnOffset;

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

    private sealed class PowerShellRunStepFinder() : YamlVisitorBase
    {
        public IList<(int LineIndex, int ColumnIndex, Range Range)> ScriptLocations { get; } = [];

        public override void Visit(YamlMappingNode mapping)
        {
            base.Visit(mapping);
        }

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

                    var shell = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "shell" });

                    if (shell.Key == default || shell.Value is not YamlScalarNode { Value: "pwsh" })
                    {
                        continue;
                    }

                    var run = step.Children.FirstOrDefault((p) => p.Key is YamlScalarNode { Value: "run" }).Value;

                    if (run != default)
                    {
                        int columnOffset = run.Start.Line == run.End.Line ? run.Start.Column - 1 : 0;
                        ScriptLocations.Add((run.Start.Line - 1, columnOffset, new(run.Start.Index, run.End.Index)));
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
