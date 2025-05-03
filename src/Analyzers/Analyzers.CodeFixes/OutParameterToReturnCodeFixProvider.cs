using System;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OutParameterToReturnCodeFixProvider)), Shared]
    public class OutParameterToReturnCodeFixProvider : ParameterAnalyzerCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(OutParameterToReturnAnalyzer.DiagnosticId); }
        }

        protected override string Title => CodeFixResources.OutParameterToReturnCodeFixTitle;

        protected override string EquivalenceKey => nameof(CodeFixResources.OutParameterToReturnCodeFixTitle);

        protected override SyntaxKind FixedModifier => SyntaxKind.OutKeyword;

        protected override SyntaxNode GetNodeFromEdit(UpdateContext updateContext, Edit edit, SyntaxNode root)
        {
            var node = root.FindNode(edit.Span);
            switch (edit)
            {
                case ParameterEdit _:
                    return node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                case ReferencedMethodEdit _:
                    return node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                default:
                    throw new Exception("Invalid edit type");
            }
        }

        protected override CSharpSyntaxRewriter CreateSyntaxRewriter(UpdateContext updateContext, ImmutableHashSet<SyntaxNode> nodes)
            => new OutParameterToReturnSyntaxRewriter(nodes, updateContext.ParameterIndex);

        class OutParameterToReturnSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly ImmutableHashSet<SyntaxNode> nodes;
            private readonly int parameterIndex;

            public OutParameterToReturnSyntaxRewriter(ImmutableHashSet<SyntaxNode> nodes, int parameterIndex)
            {
                this.nodes = nodes;
                this.parameterIndex = parameterIndex;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var newNode = base.VisitInvocationExpression(node);
                if (!nodes.Contains(node))
                {
                    return newNode;
                }

                if (node.ArgumentList.Arguments.Count <= parameterIndex)
                {
                    return newNode;
                }

                var argument = node.ArgumentList.Arguments[parameterIndex];

                var newInvocationExpression = node.WithArgumentList(
                    node.ArgumentList.WithArguments(node.ArgumentList.Arguments.Remove(argument)))
                    .WithoutLeadingTrivia();

                return SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    argument.Expression,
                    newInvocationExpression).WithLeadingTrivia(node.GetLeadingTrivia());
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var newNode = base.VisitMethodDeclaration(node);
                if (!(newNode is MethodDeclarationSyntax methodSyntax) || !nodes.Contains(node))
                {
                    return newNode;
                }

                var parameter = methodSyntax.ParameterList.Parameters[parameterIndex];

                var bodyStatements = methodSyntax.Body.Statements
                    .Insert(0, SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(parameter.Type,
                            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(parameter.Identifier)))))
                    .Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(parameter.Identifier)));

                return methodSyntax
                    .WithReturnType(parameter.Type.WithLeadingTrivia(methodSyntax.ReturnType.GetLeadingTrivia()))
                    .WithParameterList(
                        methodSyntax.ParameterList.WithParameters(methodSyntax.ParameterList.Parameters.Remove(parameter)))
                    .WithBody(SyntaxFactory.Block(bodyStatements));
            }
        }
    }
}
