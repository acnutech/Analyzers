using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertRefToOutParameterCodeFixProvider)), Shared]
    public class ConvertRefToOutParameterCodeFixProvider : ParameterAnalyzerCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzer.ConvertRefToOutParameterDiagnostic.Id); }
        }

        protected override string Title => CodeFixResources.ConvertRefToOutParameterCodeFixTitle;

        protected override string EquivalenceKey => nameof(CodeFixResources.ConvertRefToOutParameterCodeFixTitle);

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
            => new RefToOutSyntaxRewriter(nodes);

        class RefToOutSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly ImmutableHashSet<SyntaxNode> nodes;

            public RefToOutSyntaxRewriter(ImmutableHashSet<SyntaxNode> nodes)
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

                var refModifier = newParameter.Modifiers[0];

                return newParameter.WithModifiers(
                      SyntaxFactory.TokenList(
                          SyntaxFactory.Token(
                              SyntaxKind.OutKeyword)
                                .WithTriviaFrom(refModifier)));
            }

            public override SyntaxNode VisitArgument(ArgumentSyntax node)
            {
                var newNode = base.VisitArgument(node);
                if (!nodes.Contains(node))
                {
                    return newNode;
                }

                if (!node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                {
                    return newNode;
                }

                var argument = node;
                if (!argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    || !(argument.Expression is IdentifierNameSyntax))
                {
                    return newNode;
                }

                return argument
                    .WithRefKindKeyword(
                        SyntaxFactory.Token(SyntaxKind.OutKeyword)
                        .WithTriviaFrom(argument.RefKindKeyword));
            }
        }
    }
}
