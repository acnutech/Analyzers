using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acnutech.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DuplicateMethodCallAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "ACNU0003";

        private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_Title));
        private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_MessageFormat));
        private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_Description));

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule
            = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
            context.RegisterSyntaxNodeAction(AnalyzeConditionalExpression, SyntaxKind.ConditionalExpression);
        }

        private void AnalyzeConditionalExpression(SyntaxNodeAnalysisContext context)
        {
            var conditionalExpressionSyntax = (ConditionalExpressionSyntax)context.Node;

            var invocationInWhenTrue = conditionalExpressionSyntax.WhenTrue as InvocationExpressionSyntax;
            var invocationInWhenFalse = conditionalExpressionSyntax.WhenFalse as InvocationExpressionSyntax;

            RegisterDiagnosticIfDuplicatedCall(context, invocationInWhenTrue, invocationInWhenFalse,
                conditionalExpressionSyntax.QuestionToken.GetLocation());
        }

        private void AnalyzeIf(SyntaxNodeAnalysisContext context)
        {
            var ifStatementSyntax = (IfStatementSyntax)context.Node;

            if (ifStatementSyntax.Else == null)
            {
                return;
            }

            var invocationInThenBranch = GetInvocationExpression(ifStatementSyntax.Statement);
            var invocationInElseBranch = GetInvocationExpression(ifStatementSyntax.Else.Statement);

            RegisterDiagnosticIfDuplicatedCall(context, invocationInThenBranch, invocationInElseBranch, ifStatementSyntax.IfKeyword.GetLocation());
        }

        private static void RegisterDiagnosticIfDuplicatedCall(
            SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocationInThenBranch,
            InvocationExpressionSyntax invocationInElseBranch, Location diagnosticLocation)
        {
            if (invocationInThenBranch == null || invocationInElseBranch == null)
            {
                return;
            }

            if (invocationInThenBranch.ArgumentList.Arguments.Count == 0)
            {
                return;
            }

            if (invocationInThenBranch.ArgumentList.Arguments.Count != invocationInElseBranch.ArgumentList.Arguments.Count)
            {
                return;
            }


            if (!(context.SemanticModel.GetSymbolInfo(invocationInThenBranch).Symbol is IMethodSymbol thenBranchMethodSymbol)
                || !(context.SemanticModel.GetSymbolInfo(invocationInElseBranch).Symbol is IMethodSymbol elseBranchMethodSymbol))
            {
                return;
            }

            if (!SymbolEqualityComparer.Default.Equals(thenBranchMethodSymbol, elseBranchMethodSymbol))
            {
                return;
            }

            var matchingArguments = invocationInThenBranch.ArgumentList.Arguments
                .Zip(invocationInElseBranch.ArgumentList.Arguments, AreArgumentsEquivalent)
                .ToList();
            if (matchingArguments.Contains(ArgumentComparisonResult.NotComparable))
            {
                return;
            }

            if (matchingArguments.Where(a => a == ArgumentComparisonResult.Different).Count() != 1)
            {
                // Not exactly one argument is different
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, diagnosticLocation, thenBranchMethodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        internal static ArgumentComparisonResult AreArgumentsEquivalent(ArgumentSyntax a, ArgumentSyntax b)
        {
            if (a.IsEquivalentTo(b, false))
            {
                return ArgumentComparisonResult.Equivalent;
            }

            if (a.NameColon?.Name.Identifier.Text != b.NameColon?.Name.Identifier.Text)
            {
                return ArgumentComparisonResult.NotComparable;
            }

            return ArgumentComparisonResult.Different;
        }

        internal enum ArgumentComparisonResult
        {
            Equivalent,
            Different,
            NotComparable
        }

        private InvocationExpressionSyntax GetInvocationExpression(CSharpSyntaxNode node)
        {
            if (node is BlockSyntax blockSyntax)
            {
                node = blockSyntax.Statements.SingleOrDefault();
            }

            return node?.ChildNodes().SingleOrDefault() as InvocationExpressionSyntax;
        }

        private static LocalizableResourceString GetLocalizableString(string name)
            => new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));
    }
}
