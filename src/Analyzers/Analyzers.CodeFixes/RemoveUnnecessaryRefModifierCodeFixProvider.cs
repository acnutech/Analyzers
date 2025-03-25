using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveUnnecessaryRefModifierCodeFixProvider)), Shared]
    public class RemoveUnnecessaryRefModifierCodeFixProvider : RefParameterAnalyzerCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Id); }
        }

        protected override string Title => CodeFixResources.CodeFixTitle;

        protected override string EquivalenceKey => nameof(CodeFixResources.CodeFixTitle);

        protected override void UpdateRefParameter(DocumentEditor documentEditor, SyntaxNode node)
        {
            var parameterSyntax1 = (ParameterSyntax)node;

            var refModifier1 = parameterSyntax1.Modifiers[0];
            var parameterTrivia = GetTriviaAfterNodeRemoval(refModifier1, parameterSyntax1.Type.GetLeadingTrivia());

            var newParameter = parameterSyntax1.WithModifiers(SyntaxFactory.TokenList())
                .WithType(parameterSyntax1.Type.WithLeadingTrivia(parameterTrivia))
                .WithAdditionalAnnotations(Formatter.Annotation);

            documentEditor.ReplaceNode(node, newParameter);
        }

        protected override void UpdateRefArgument(DocumentEditor documentEditor, SyntaxNode node, int refParameterIndex)
        {
            var invocationExpression = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count <= refParameterIndex)
            {
                return;
            }

            var argument = arguments[refParameterIndex];

            if (!argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || !(argument.Expression is IdentifierNameSyntax identifier))
            {
                return;
            }

            var trivia = GetTriviaAfterNodeRemoval(argument.RefKindKeyword, identifier.Identifier.LeadingTrivia);

            var argumentWithoutRef =
                argument
                    .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None))
                    .WithLeadingTrivia(trivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

            documentEditor.ReplaceNode(
                invocationExpression,
                invocationExpression.WithArgumentList(
                    invocationExpression.ArgumentList.ReplaceNode(
                        argument,
                        argumentWithoutRef)));
        }

        private static SyntaxTriviaList GetTriviaAfterNodeRemoval(SyntaxToken removedNode, SyntaxTriviaList nextLeadingTrivia)
            => removedNode.LeadingTrivia
                .AddRange(NonTrivialTrivia(removedNode.TrailingTrivia))
                .AddRange(nextLeadingTrivia);

        private static SyntaxTriviaList NonTrivialTrivia(SyntaxTriviaList trivia)
            => trivia.Count == 1
                && trivia[0].IsKind(SyntaxKind.WhitespaceTrivia)
                && trivia[0].ToString() == " "
                ? SyntaxFactory.TriviaList()
                : trivia;
    }
}
