using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertRefToOutParameterCodeFixProvider)), Shared]
    public class ConvertRefToOutParameterCodeFixProvider : RefParameterAnalyzerCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzer.ConvertRefToOutParameterDiagnostic.Id); }
        }

        protected override string Title => "aa";

        protected override string EquivalenceKey => "bgb";

        protected override void UpdateRefParameter(DocumentEditor documentEditor, SyntaxNode node)
        {
            var parameterSyntax = (ParameterSyntax)node;

            var refModifier = parameterSyntax.Modifiers[0];

            var newParameter = parameterSyntax.WithModifiers(
                  SyntaxFactory.TokenList(
                      SyntaxFactory.Token(
                          SyntaxKind.OutKeyword)
                            .WithTriviaFrom(refModifier)));

            documentEditor.ReplaceNode(node, newParameter);
        }

        override protected void UpdateRefArgument(DocumentEditor documentEditor, SyntaxNode node, int refParameterIndex)
        {
            var invocationExpression = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count <= refParameterIndex)
            {
                return;
            }

            var argument = arguments[refParameterIndex];

            if (!argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || !(argument.Expression is IdentifierNameSyntax))
            {
                return;
            }

            var argumentWithoutRef =
                argument
                    .WithRefKindKeyword(
                        SyntaxFactory.Token(SyntaxKind.OutKeyword)
                        .WithTriviaFrom(argument.RefKindKeyword));

            documentEditor.ReplaceNode(
                invocationExpression,
                invocationExpression.WithArgumentList(
                    invocationExpression.ArgumentList.ReplaceNode(
                        argument,
                        argumentWithoutRef)));
        }
    }
}
