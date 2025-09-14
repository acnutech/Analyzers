using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantConvertCodeFixProvider)), Shared]
    public sealed class RedundantConvertCodeFixProvider : CodeFixProvider
    {
        public override FixAllProvider GetFixAllProvider()
        {
            //Debugger.Launch();
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(RedundantConvertAnalyzer.DiagnosticId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var invocationExpr = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();

            if (IsReturnValueUsed(invocationExpr) != true)
            {
                // Only offer the code fix if the return value is used.
                return;
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.RedundantConvertCodeFixTitle,
                    createChangedSolution:
                        async c => await RemoveRedundantConvertCall(context.Document, invocationExpr, c),
                    equivalenceKey: nameof(CodeFixResources.RedundantConvertCodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> RemoveRedundantConvertCall(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken c)
        {
            var editor = await DocumentEditor.CreateAsync(document, default);
            var argument = invocationExpr.ArgumentList.Arguments[0];
            var updatedExpression = argument.Expression
                .WithLeadingTrivia(invocationExpr.GetLeadingTrivia()
                    .AddRange(invocationExpr.ArgumentList.OpenParenToken.TrailingTrivia)
                    .AddRange(argument.GetLeadingTrivia()))
                .WithTrailingTrivia(argument.GetTrailingTrivia()
                    .AddRange(invocationExpr.ArgumentList.CloseParenToken.LeadingTrivia)
                    .AddRange(invocationExpr.GetTrailingTrivia()));
            editor.ReplaceNode(invocationExpr, updatedExpression);
            return editor.GetChangedDocument().Project.Solution;
        }

        private static bool? IsReturnValueUsed(InvocationExpressionSyntax invocationExpr)
        {
            var parent = invocationExpr.Parent;
            if (parent is ExpressionStatementSyntax statementSyntax && statementSyntax.Expression == invocationExpr)
            {
                // The invocation is a standalone statement, so its return value is not used.
                return false;
            }

            switch (parent)
            {
                case ArgumentSyntax _:
                case EqualsValueClauseSyntax _:
                case ReturnStatementSyntax _:
                case BinaryExpressionSyntax _:
                case ParenthesizedExpressionSyntax _:
                case MemberAccessExpressionSyntax _:
                case ConditionalExpressionSyntax _:
                case ConditionalAccessExpressionSyntax conditionalAccess
                    when conditionalAccess.Expression == invocationExpr:
                    return true;
                default:
                    // Unable to determine usage; return null
                    return default;
            }
        }
    }
}
