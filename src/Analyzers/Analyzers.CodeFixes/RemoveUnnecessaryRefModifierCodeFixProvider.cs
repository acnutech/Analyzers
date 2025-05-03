using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveUnnecessaryRefModifierCodeFixProvider)), Shared]
    public class RemoveUnnecessaryRefModifierCodeFixProvider : ParameterAnalyzerCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Id); }
        }

        protected override string Title => CodeFixResources.RemoveRefModifierCodeFixTitle;

        protected override string EquivalenceKey => nameof(CodeFixResources.RemoveRefModifierCodeFixTitle);

        protected override SyntaxKind FixedModifier => SyntaxKind.RefKeyword;

        protected override SyntaxNode GetNodeFromEdit(UpdateContext updateContext, Edit edit, SyntaxNode root)
        {
            var node = root.FindNode(edit.Span);
            switch (edit)
            {
                case ParameterEdit _:
                    return node.FirstAncestorOrSelf<MethodDeclarationSyntax>()
                        .ParameterList.Parameters.ElementAtOrDefault(updateContext.ParameterIndex);
                case ReferencedMethodEdit _:
                    return node.FirstAncestorOrSelf<InvocationExpressionSyntax>()
                        .ArgumentList.Arguments.ElementAtOrDefault(updateContext.ParameterIndex);
                default:
                    throw new Exception("Invalid edit type");
            }
        }

        protected override CSharpSyntaxRewriter CreateSyntaxRewriter(UpdateContext updateContext, ImmutableHashSet<SyntaxNode> nodes)
            => new RemoveRefSyntaxRewriter(nodes);

        class RemoveRefSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly ImmutableHashSet<SyntaxNode> nodes;

            public RemoveRefSyntaxRewriter(ImmutableHashSet<SyntaxNode> nodes)
            {
                this.nodes = nodes;
            }

            public override SyntaxNode VisitParameter(ParameterSyntax node)
            {
                var newNode = base.VisitParameter(node);

                if (!(newNode is ParameterSyntax newParameter) || !nodes.Contains(node))
                {
                    return newNode;
                }

                var refModifier1 = newParameter.Modifiers[0];
                var parameterTrivia = GetTriviaAfterNodeRemoval(refModifier1, newParameter.Type.GetLeadingTrivia());

                return newParameter.WithModifiers(SyntaxFactory.TokenList())
                    .WithType(newParameter.Type.WithLeadingTrivia(parameterTrivia))
                    .WithAdditionalAnnotations(Formatter.Annotation);
            }

            public override SyntaxNode VisitArgument(ArgumentSyntax node)
            {
                var newNode = base.VisitArgument(node);

                if (!(newNode is ArgumentSyntax newArgument) || !nodes.Contains(node))
                {
                    return newNode;
                }

                if (!newArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                {
                    return newArgument;
                }

                if (!newArgument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                         || !(newArgument.Expression is IdentifierNameSyntax identifier))
                {
                    return newArgument;
                }

                var trivia = GetTriviaAfterNodeRemoval(newArgument.RefKindKeyword, identifier.Identifier.LeadingTrivia);

                return newArgument
                        .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None))
                        .WithLeadingTrivia(trivia)
                        .WithAdditionalAnnotations(Formatter.Annotation);
            }
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
