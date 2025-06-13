using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Acnutech.Analyzers.OutParametersToTupleCodeFixProvider;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OutParametersToTupleCodeFixProvider)), Shared]
    public class OutParametersToTupleCodeFixProvider
        : MethodDeclarationCodeFixProvider<OutParametersToTupleUpdateContext, MethodDeclarationSyntax>
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(OutParameterToReturnAnalyzer.MultipleOutParametersDiagnostic.Id); }
        }

        protected override string Title => CodeFixResources.OutParameterToReturnCodeFixTitle;

        protected override string EquivalenceKey => nameof(CodeFixResources.OutParameterToReturnCodeFixTitle);


        public OutParametersToTupleCodeFixProvider()
        {

        }

        protected override MethodDeclarationSyntax MapSpanToEditNode(SyntaxNode root, TextSpan span)
            => root.FindToken(span.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

        protected override bool TryGetMethodDeclarationSyntax(MethodDeclarationSyntax node, out MethodDeclarationSyntax methodDeclarationSyntax)
        {
            methodDeclarationSyntax = node;
            return methodDeclarationSyntax != null;
        }

        protected override SyntaxNode GetNodeFromEdit(OutParametersToTupleUpdateContext updateContext, Edit edit, SyntaxNode root)
        {
            var node = root.FindNode(edit.Span);
            switch (edit)
            {
                case PrimaryEdit _:
                    return node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                case ReferencedMethodEdit _:
                    return node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                default:
                    throw new Exception("Invalid edit type");
            }
        }

        protected override OutParametersToTupleUpdateContext CreateContext(MethodDeclarationSyntax primaryEditNode, MethodDeclarationSyntax methodDeclarationSyntax, IMethodSymbol methodSymbol)
        {
            var parameterIndexes =
                from parameterWithIndex in methodDeclarationSyntax.ParameterList.Parameters.Select((p, i) => (Parameter: p, Index: i))
                where parameterWithIndex.Parameter.Modifiers.Any(SyntaxKind.OutKeyword)
                select parameterWithIndex.Index;
            return new OutParametersToTupleUpdateContext(parameterIndexes.ToImmutableArray());
        }

        protected override CSharpSyntaxRewriter CreateSyntaxRewriter(OutParametersToTupleUpdateContext updateContext, ImmutableHashSet<SyntaxNode> nodes)
            => new OutParametersToTupleSyntaxRewriter(nodes, updateContext.ParameterIndexes);

        class OutParametersToTupleSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly ImmutableHashSet<SyntaxNode> nodes;
            private readonly ImmutableArray<int> parameterIndexes;

            public OutParametersToTupleSyntaxRewriter(ImmutableHashSet<SyntaxNode> nodes, ImmutableArray<int> parameterIndexes)
            {
                this.nodes = nodes;
                this.parameterIndexes = parameterIndexes;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var newNode = base.VisitInvocationExpression(node);
                if (!(newNode is InvocationExpressionSyntax invocationExpressionSyntax) || !nodes.Contains(node))
                {
                    return newNode;
                }

                var allArguments
                    = invocationExpressionSyntax.ArgumentList.GetArgumentsWithSeparators().ToList();

                var indexes = parameterIndexes.ToImmutableHashSet();

                var mappedArguments = parameterIndexes
                    .Select(i => allArguments.ElementAtOrDefault(i));
                var withoutOutKeyword = mappedArguments
                    .Select(WithoutOutKeyword);

                var leftArguments = allArguments
                    .Select((a, i) => (Argument: a, Index: i))
                    .Where(a => !parameterIndexes.Contains(a.Index))
                    .Select(a => a.Argument);

                var newInvocationExpression = invocationExpressionSyntax.WithArgumentList(
                    node.ArgumentList.WithArguments(leftArguments.ToSeparatedSyntaxList()))
                    .WithoutLeadingTrivia();
                var returnTuple
                    = SyntaxFactory.TupleExpression(
                        SyntaxFactory.Token(SyntaxKind.OpenParenToken),
                        withoutOutKeyword.ToSeparatedSyntaxList(),
                        SyntaxFactory.Token(SyntaxKind.CloseParenToken));

                SeparatedSyntaxList<ArgumentSyntax> arguments = newInvocationExpression.ArgumentList.Arguments;

                return SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    returnTuple,
                    newInvocationExpression).WithLeadingTrivia(node.GetLeadingTrivia());

                SyntaxNodeWithSeparator<ArgumentSyntax> WithoutOutKeyword(SyntaxNodeWithSeparator<ArgumentSyntax> argument)
                    => new SyntaxNodeWithSeparator<ArgumentSyntax>(
                        argument.SyntaxNode.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None)),
                        argument.Separator);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var newNode = base.VisitMethodDeclaration(node);
                if (!(newNode is MethodDeclarationSyntax methodSyntax) || !nodes.Contains(node))
                {
                    return newNode;
                }

                var parameters = methodSyntax.ParameterList.Parameters;

                var outParameters =
                    from index in parameterIndexes
                    let parameter = parameters.ElementAtOrDefault(index)
                    where !(parameter is null)
                    select parameter;

                var tupleElements =
                    from parameter in outParameters
                    select SyntaxFactory.TupleElement(parameter.Type, parameter.Identifier);

                var dd = SyntaxFactory.TupleType(SyntaxFactory.SeparatedList<TupleElementSyntax>(tupleElements));

                var declarations
                    = from parameter in outParameters
                      select SyntaxFactory.LocalDeclarationStatement(
                          SyntaxFactory.VariableDeclaration(parameter.Type,
                              SyntaxFactory.SingletonSeparatedList(SyntaxFactory.VariableDeclarator(parameter.Identifier))));

                var bodyStatements = methodSyntax.Body.Statements
                   .InsertRange(0, declarations)
                   .Add(SyntaxFactory.ReturnStatement(
                       SyntaxFactory.TupleExpression(
                            SyntaxFactory.SeparatedList(
                                 outParameters.Select(i => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(i.Identifier)))))));


                return methodSyntax
                    .WithReturnType(dd)
                    .WithParameterList(
                        methodSyntax.ParameterList.WithParameters(methodSyntax.ParameterList.Parameters.Without(p => p.Modifiers.Any(SyntaxKind.OutKeyword))))
                    .WithBody(SyntaxFactory.Block(bodyStatements));
            }

            void dd(out int d)
            {
                dd(out d);
            }

            void dd2(out int d)
            {
                dd(out int r);
                d = r;
            }
            (int d, int e) aa()
            {
                int a = 9, b = 0;

                (a, b) = aa();

                return (a, b);
            }
        }

        public class OutParametersToTupleUpdateContext : UpdateContext
        {
            public OutParametersToTupleUpdateContext(ImmutableArray<int> parameterIndexes)
            {
                ParameterIndexes = parameterIndexes;
            }

            public ImmutableArray<int> ParameterIndexes { get; }
        }
    }
}
