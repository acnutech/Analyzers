using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acnutech.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RedundantConvertAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "ACNU0005";

        private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.RedundantConvertDiagnostic_Title));
        private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.RedundantConvertDiagnostic_MessageFormat));
        private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.RedundantConvertDiagnostic_Description));

        private const string Category = "Usage";

        internal static readonly DiagnosticDescriptor Rule
            = new DiagnosticDescriptor(
                DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true,
                description: Description, helpLinkUri: "https://github.com/acnutech/Analyzers/wiki/ACNU0005");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeInvocationExpression, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = (InvocationExpressionSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var methodSymbol = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (methodSymbol == null)
            {
                return;
            }

            if (methodSymbol.ContainingType.ToDisplayString() != "System.Convert")
            {
                return;
            }

            if (!methodSymbol.Name.StartsWith("To", StringComparison.Ordinal))
            {
                return;
            }

            if (invocationExpression.ArgumentList.Arguments.Count != 1)
            {
                return;
            }

            var argument = invocationExpression.ArgumentList.Arguments[0];
            if (argument == null)
            {
                return;
            }

            var argumentType = semanticModel.GetTypeInfo(argument.Expression);
            if (argumentType.Type == null)
            {
                return;
            }

            if (!SymbolEqualityComparer.Default.Equals(argumentType.Type, methodSymbol.ReturnType))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, invocationExpression.Expression.GetLocation(), methodSymbol.Name, argumentType.Type.ToDisplayString());
            context.ReportDiagnostic(diagnostic);
        }

        private static LocalizableResourceString GetLocalizableString(string name)
            => new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));
    }
}
