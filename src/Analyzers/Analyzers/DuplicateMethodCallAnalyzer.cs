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
        internal const string Id = "ACNU0003";

        private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_Title));
        private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_MessageFormat));
        private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.DuplicateMethodCallDiagnostic_Description));

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule
            = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        }

        private void AnalyzeIf(SyntaxNodeAnalysisContext context)
        {
            var ifStatementSyntax = (IfStatementSyntax)context.Node;

            if (ifStatementSyntax.Else == null)
            {
                return;
            }

            var invocationInStatement = GetInvocationExpression(ifStatementSyntax.Statement);
            var invocationInElse = GetInvocationExpression(ifStatementSyntax.Else.Statement);

            if (invocationInStatement == null || invocationInElse == null)
            {
                return;
            }

            if (invocationInStatement.ArgumentList.Arguments.Count != invocationInElse.ArgumentList.Arguments.Count)
            {
                return;
            }

            var statementMethodSymbol = context.SemanticModel.GetSymbolInfo(invocationInStatement).Symbol as IMethodSymbol;
            var elseMethodSymbol = context.SemanticModel.GetSymbolInfo(invocationInElse).Symbol as IMethodSymbol;

            if (statementMethodSymbol == null || elseMethodSymbol == null)
            {
                return;
            }

            if (!SymbolEqualityComparer.Default.Equals(statementMethodSymbol, elseMethodSymbol))
            {
                return;
            }

            var matchingArguments = invocationInStatement.ArgumentList.Arguments.Zip(
                invocationInElse.ArgumentList.Arguments,
                (a, b) => a.Expression.IsEquivalentTo(b.Expression, false));
            if (matchingArguments.Where(a => !a).Count() > 1)
            {
                // more then one different argument
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, ifStatementSyntax.IfKeyword.GetLocation(), statementMethodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
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
