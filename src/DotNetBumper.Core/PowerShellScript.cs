// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Management.Automation.Language;
using MartinCostello.DotNetBumper.Upgraders;

namespace MartinCostello.DotNetBumper;

internal sealed class PowerShellScript
{
    private PowerShellScript(
        FileMetadata fileMetadata,
        List<string> lines,
        Ast syntaxTree)
    {
        FileMetadata = fileMetadata;
        Lines = lines;
        SyntaxTree = syntaxTree;
    }

    public FileMetadata FileMetadata { get; }

    public List<string> Lines { get; }

    public Ast SyntaxTree { get; }

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

        return new PowerShellScript(metadata, lines, syntaxTree);
    }

    public bool TryUpdate(Version channel)
    {
        var vistor = new SyntaxTreeVisitor(channel);
        SyntaxTree.Visit(vistor);

        if (vistor.Edits.Count is 0)
        {
            return false;
        }

        foreach (var lineEdits in vistor.Edits.GroupBy((p) => p.Location.StartLineNumber))
        {
            int lineIndex = lineEdits.Key - 1;

            var edits = lineEdits
                .OrderBy((p) => p.Location.StartOffset)
                .ToList();

            var builder = new StringBuilder();

            int offset = 0;

            var original = Lines[lineIndex];

            foreach ((var location, var replacement) in edits)
            {
                int start = location.StartColumnNumber - 1;
                int end = location.EndColumnNumber - 1;

                builder.Append(original[offset..start]);
                builder.Append(replacement);

                offset = end;
            }

            builder.Append(original[offset..]);

            Lines[lineIndex] = builder.ToString();
        }

        return true;
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
