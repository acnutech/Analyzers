using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acnutech.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RefParameterAnalyzer : DiagnosticAnalyzer
    {
        internal static class RemoveUnnecessaryRefModifierDiagnostic
        {
            internal const string Id = "ACNU0001";

            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
            private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.AnalyzerTitle));
            private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.AnalyzerMessageFormat));
            private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.AnalyzerDescription));
            private const string Category = "Usage";

            internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description,
                helpLinkUri: "https://github.com/acnutech/Analyzers/wiki/ACNU0001");
        }

        internal static class ConvertRefToOutParameterDiagnostic
        {
            internal const string Id = "ACNU0002";

            private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.ConvertRefToOutParameterDiagnostic_Title));
            private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.ConvertRefToOutParameterDiagnostic_MessageFormat));
            private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.ConvertRefToOutParameterDiagnostic_Description));

            private const string Category = "Usage";

            internal static readonly DiagnosticDescriptor Rule
                = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description,
                    helpLinkUri: "https://github.com/acnutech/Analyzers/wiki/ACNU0002");
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(
                 ConvertRefToOutParameterDiagnostic.Rule,
                 RemoveUnnecessaryRefModifierDiagnostic.Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.Parameter);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            // https://github.com/PacktPublishing/Roslyn-Cookbook/blob/master/Chapter03/CodeSamples/Recipe%204%20-%20CodeRefactoringProvider/CodeRefactoring.zip
            ParameterSyntax parameterSyntax = (ParameterSyntax)context.Node;
            if (parameterSyntax.Modifiers.Count != 1)
            {
                return;
            }

            var refModifier = parameterSyntax.Modifiers[0];
            if (!refModifier.IsKind(SyntaxKind.RefKeyword))
            {
                return;
            }

            try
            {
                // Perform data flow analysis on the parameter.
                var method = parameterSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();

                if (method.Modifiers.Any(SyntaxKind.VirtualKeyword) || method.Body == null)
                {
                    return;
                }

                if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    return;
                }

                IMethodSymbol methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
                if (methodSymbol.ExplicitInterfaceImplementations.Any() || methodSymbol.ImplementsInterfaceImplicitly())
                {
                    return;
                }

                DataFlowAnalysis dataFlowAnalysis = context.SemanticModel.AnalyzeDataFlow(method.Body);
                if (!dataFlowAnalysis.Succeeded)
                {
                    return;
                }

                ISymbol parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken);
                if (!dataFlowAnalysis.WrittenInside.Contains(parameterSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(RemoveUnnecessaryRefModifierDiagnostic.Rule, refModifier.GetLocation(), parameterSyntax.Identifier.Text));
                    return;
                }

                if (dataFlowAnalysis.AlwaysAssigned.Contains(parameterSymbol)
                    && !dataFlowAnalysis.DataFlowsIn.Contains(parameterSymbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(ConvertRefToOutParameterDiagnostic.Rule, refModifier.GetLocation(), parameterSyntax.Identifier.Text));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        private static LocalizableResourceString GetLocalizableString(string name)
            => new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));
    }
}
