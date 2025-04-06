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

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.DuplicateMethodCallCodeFixTitle,
                    createChangedSolution:
                        async c => await ConvertToMethodWithConditionalArgument(context, root.FindToken(diagnosticSpan.Start), c)
                            ?? context.Document.Project.Solution,
                    equivalenceKey: nameof(CodeFixResources.DuplicateMethodCallCodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> ConvertToMethodWithConditionalArgument(CodeFixContext context,
            SyntaxToken syntaxToken, CancellationToken cancellationToken)
        {
            var ifStatementSyntax = syntaxToken.Parent.AncestorsAndSelf()
                .OfType<IfStatementSyntax>()
                .FirstOrDefault();
            if (ifStatementSyntax != null)
            {
                var invocationInThenBranch = GetInvocationExpression(ifStatementSyntax.Statement);
                var invocationInElseBranch = GetInvocationExpression(ifStatementSyntax.Else?.Statement);
                return await ConvertToMethodWithConditionalArgument(context, invocationInThenBranch,
                    invocationInElseBranch, ifStatementSyntax.Condition, ifStatementSyntax.OpenParenToken.TrailingTrivia,
                    ifStatementSyntax, cancellationToken).ConfigureAwait(false);
            }

            var conditionalExpressionSyntax = syntaxToken.Parent.AncestorsAndSelf()
                .OfType<ConditionalExpressionSyntax>()
                .FirstOrDefault();
            if (conditionalExpressionSyntax != null)
            {
                var invocationInWhenTrue = conditionalExpressionSyntax.WhenTrue as InvocationExpressionSyntax;
                var invocationInWhenFalse = conditionalExpressionSyntax.WhenFalse as InvocationExpressionSyntax;
                return await ConvertToMethodWithConditionalArgument(
                    context, invocationInWhenTrue, invocationInWhenFalse, conditionalExpressionSyntax.Condition,
                    conditionalExpressionSyntax.GetLeadingTrivia(), conditionalExpressionSyntax, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        private async Task<Solution> ConvertToMethodWithConditionalArgument(CodeFixContext context,
            InvocationExpressionSyntax thanBranchInvocation, InvocationExpressionSyntax elseBranchInvocation,
            ExpressionSyntax condition, SyntaxTriviaList conditionProceedingTrivia, SyntaxNode replacedSyntaxNode, CancellationToken cancellationToken)
        {
            if (thanBranchInvocation == null || elseBranchInvocation == null)
            {
                return null;
            }

            if (thanBranchInvocation.ArgumentList.Arguments.Count == 0)
            {
                return null;
            }

            if (thanBranchInvocation.ArgumentList.Arguments.Count != elseBranchInvocation.ArgumentList.Arguments.Count)
            {
                return null;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken);

            if (!(semanticModel.GetSymbolInfo(thanBranchInvocation).Symbol is IMethodSymbol statementMethodSymbol)
                || !(semanticModel.GetSymbolInfo(elseBranchInvocation).Symbol is IMethodSymbol elseMethodSymbol))
            {
                return null;
            }

            if (!SymbolEqualityComparer.Default.Equals(statementMethodSymbol, elseMethodSymbol))
            {
                return null;
            }

            (var leadingWhitespace, var leadingNonWhitespace)
                = SplitLeadingTrivia(conditionProceedingTrivia);

            var conditionWithTrivia = new WithTrivia<ExpressionSyntax>(
                condition,
                leadingNonWhitespace);
            var matchingArguments =
                Arguments(thanBranchInvocation.ArgumentList)
                    .Zip(Arguments(elseBranchInvocation.ArgumentList),
                    (a, b) => JoinArguments(a, b, conditionWithTrivia));

            var ifReplacement =
                SyntaxFactory.InvocationExpression(
                    thanBranchInvocation.Expression.WithoutTrivia(),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.Token(SyntaxKind.OpenParenToken).WithTrailingTrivia(matchingArguments.First().Trivia),
                        SyntaxFactory.SeparatedList(
                            matchingArguments.Select(a => a.Syntax),
                            GetSeparators(matchingArguments)),
                        SyntaxFactory.Token(SyntaxKind.CloseParenToken)))
                    .WithLeadingTrivia(leadingWhitespace)
                    .WithTrailingTrivia(replacedSyntaxNode.GetTrailingTrivia());

            var oldRoot = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var replacementNode =
                replacedSyntaxNode is StatementSyntax
                ? (SyntaxNode)SyntaxFactory.ExpressionStatement(ifReplacement)
                : ifReplacement;

            var newRoot =
                oldRoot
                .ReplaceNode(
                    replacedSyntaxNode,
                    replacementNode
                        .WithAdditionalAnnotations(Formatter.Annotation)
                );

            var updatedDocument = context.Document.WithSyntaxRoot(newRoot);
            var formattedDocument = await Formatter.FormatAsync(updatedDocument, Formatter.Annotation, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return formattedDocument.Project.Solution;
        }

        private (SyntaxTriviaList LeadingWhitespace, SyntaxTriviaList LeadingNonWhitespace) SplitLeadingTrivia(SyntaxTriviaList trivia)
        {
            var leadingWhitespace = new List<SyntaxTrivia>();
            var leadingNonWhitespace = new List<SyntaxTrivia>();
            bool isLeadingWhitespace = true;
            foreach (var t in trivia)
            {
                if (isLeadingWhitespace
                    && t.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    leadingWhitespace.Add(t);
                }
                else if (t.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    isLeadingWhitespace = true;
                    leadingWhitespace.AddRange(leadingNonWhitespace);
                    leadingWhitespace.Add(t);
                    leadingNonWhitespace.Clear();
                }
                else
                {
                    isLeadingWhitespace = false;
                    leadingNonWhitespace.Add(t);
                }
            }

            return (SyntaxFactory.TriviaList(leadingWhitespace),
                SyntaxFactory.TriviaList(leadingNonWhitespace));
        }

        private IEnumerable<SyntaxToken> GetSeparators(IEnumerable<WithTrivia<ArgumentSyntax>> arguments)
            => from argument in arguments.Skip(1)
               select SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(argument.Trivia);

        private SyntaxTriviaList WithoutLastEndOfLine(SyntaxTriviaList syntaxTrivias)
            => syntaxTrivias.Count > 0
                && syntaxTrivias.Last().IsKind(SyntaxKind.EndOfLineTrivia)
                ? syntaxTrivias.RemoveAt(syntaxTrivias.Count - 1)
                : syntaxTrivias;

        private InvocationExpressionSyntax GetInvocationExpression(CSharpSyntaxNode node)
        {
            if (node is BlockSyntax blockSyntax)
            {
                node = blockSyntax.Statements.SingleOrDefault();
            }

            return node?.ChildNodes().SingleOrDefault() as InvocationExpressionSyntax;
        }

        private WithTrivia<ArgumentSyntax> JoinArguments(WithTrivia<ArgumentSyntax> argument1,
            WithTrivia<ArgumentSyntax> argument2, WithTrivia<ExpressionSyntax> conditionExpression)
        {
            switch (DuplicateMethodCallAnalyzer.AreArgumentsEquivalent(argument1.Syntax, argument2.Syntax))
            {
                case DuplicateMethodCallAnalyzer.ArgumentComparisonResult.Equivalent:
                    return argument1;

                case DuplicateMethodCallAnalyzer.ArgumentComparisonResult.Different:
                    return new WithTrivia<ArgumentSyntax>(
                        SyntaxFactory.Argument(
                            argument1.Syntax.NameColon,
                            SyntaxFactory.Token(SyntaxKind.None),
                            SyntaxFactory.ConditionalExpression(
                                conditionExpression.Syntax
                                    .WithoutLeadingTrivia(),
                                SyntaxFactory.Token(SyntaxKind.QuestionToken)
                                    .WithTrailingTrivia(argument1.Trivia),
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

        bool NeedsLeadingTrivia(SyntaxTriviaList previousTrailingTrivia)
            => previousTrailingTrivia.Count > 0
                && previousTrailingTrivia.Last().IsKind(SyntaxKind.EndOfLineTrivia);

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
