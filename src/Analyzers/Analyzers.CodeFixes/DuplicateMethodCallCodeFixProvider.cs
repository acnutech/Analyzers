using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConvertRefToOutParameterCodeFixProvider)), Shared]
    public sealed class DuplicateMethodCallCodeFixProvider : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(DuplicateMethodCallAnalyzer.DiagnosticId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<IfStatementSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.DuplicateMethodCallCodeFixTitle,
                    createChangedSolution:
                        async c => await ConvertToMethodWithConditionalArgument(context, declaration, c)
                            ?? context.Document.Project.Solution,
                    equivalenceKey: nameof(CodeFixResources.DuplicateMethodCallCodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> ConvertToMethodWithConditionalArgument(CodeFixContext context, IfStatementSyntax ifStatementSyntax, CancellationToken cancellationToken)
        {
            var invocationInStatement = GetInvocationExpression(ifStatementSyntax.Statement);
            var invocationInElse = GetInvocationExpression(ifStatementSyntax.Else?.Statement);

            if (invocationInStatement == null || invocationInElse == null)
            {
                return null;
            }

            if (invocationInStatement.ArgumentList.Arguments.Count == 0)
            {
                return null;
            }

            if (invocationInStatement.ArgumentList.Arguments.Count != invocationInElse.ArgumentList.Arguments.Count)
            {
                return null;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);

            if (!(semanticModel.GetSymbolInfo(invocationInStatement).Symbol is IMethodSymbol statementMethodSymbol)
                || !(semanticModel.GetSymbolInfo(invocationInElse).Symbol is IMethodSymbol elseMethodSymbol))
            {
                return null;
            }

            if (!SymbolEqualityComparer.Default.Equals(statementMethodSymbol, elseMethodSymbol))
            {
                return null;
            }

            var conditionWithTrivia = new WithTrivia<ExpressionSyntax>(
                ifStatementSyntax.Condition,
                ifStatementSyntax.OpenParenToken.TrailingTrivia);
            var matchingArguments = Arguments(invocationInStatement.ArgumentList)
                .Zip(Arguments(invocationInElse.ArgumentList),
                (a, b) => JoinArguments(a, b, conditionWithTrivia));

            var ifReplacement =
                SyntaxFactory.InvocationExpression(
                    invocationInStatement.Expression.WithoutTrivia(),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.Token(SyntaxKind.OpenParenToken).WithTrailingTrivia(matchingArguments.First().Trivia),
                        SyntaxFactory.SeparatedList(
                            matchingArguments.Select(a => a.Syntax),
                            GetSeparators(matchingArguments)),
                        SyntaxFactory.Token(SyntaxKind.CloseParenToken)));

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var newRoot =
                oldRoot
                .ReplaceNode(
                    ifStatementSyntax,
                    SyntaxFactory.ExpressionStatement(ifReplacement)
                        .WithAdditionalAnnotations(Formatter.Annotation)
                );

            var updatedDocument = context.Document.WithSyntaxRoot(newRoot);
            var formattedDocument = await Formatter.FormatAsync(updatedDocument, Formatter.Annotation, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return formattedDocument.Project.Solution;
        }

        private IEnumerable<SyntaxToken> GetSeparators(IEnumerable<WithTrivia<ArgumentSyntax>> arguments)
        {
            foreach (var argument in arguments.Skip(1))
            {
                yield return SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(argument.Trivia);
            }
        }

        private InvocationExpressionSyntax GetInvocationExpression(CSharpSyntaxNode node)
        {
            if (node is BlockSyntax blockSyntax)
            {
                node = blockSyntax.Statements.SingleOrDefault();
            }

            return node?.ChildNodes().SingleOrDefault() as InvocationExpressionSyntax;
        }

        private WithTrivia<ArgumentSyntax> JoinArguments(WithTrivia<ArgumentSyntax> argument1, WithTrivia<ArgumentSyntax> argument2, WithTrivia<ExpressionSyntax> conditionExpression)
        {
            switch (DuplicateMethodCallAnalyzer.AreArgumentsEquivalent(argument1.Syntax, argument2.Syntax))
            {
                case DuplicateMethodCallAnalyzer.ArgumentComparisonResult.Equivalent:
                    return argument1;

                case DuplicateMethodCallAnalyzer.ArgumentComparisonResult.Different:

                    return new WithTrivia<ArgumentSyntax>(
                        SyntaxFactory.Argument(
                            argument1.Syntax.NameColon,
                            argument1.Syntax.RefKindKeyword,
                            SyntaxFactory.ConditionalExpression(
                                conditionExpression.Syntax,
                                SyntaxFactory.Token(SyntaxKind.QuestionToken).WithTrailingTrivia(argument1.Trivia),
                                argument1.Syntax.Expression,
                                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(argument2.Trivia),
                                argument2.Syntax.Expression)),
                        conditionExpression.Trivia);

                case DuplicateMethodCallAnalyzer.ArgumentComparisonResult.NotComparable:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException();
            }
        }



        private IEnumerable<WithTrivia<ArgumentSyntax>> Arguments(ArgumentListSyntax argumentList)
        {
            for (var i = 0; i < argumentList.Arguments.Count; i++)
            {
                yield return new WithTrivia<ArgumentSyntax>(
                    argumentList.Arguments[i],
                    i == 0
                        ? argumentList.OpenParenToken.TrailingTrivia
                        : argumentList.Arguments.GetSeparator(i - 1).TrailingTrivia);
            }
        }

        private readonly struct WithTrivia<T>
            where T : SyntaxNode
        {
            public T Syntax { get; }

            /// <summary>
            /// The previous node trivia.
            /// </summary>
            public SyntaxTriviaList Trivia { get; }

            public WithTrivia(T syntax, SyntaxTriviaList leadingTrivia)
            {
                Syntax = syntax;
                Trivia = leadingTrivia;
            }
        }
    }
}
