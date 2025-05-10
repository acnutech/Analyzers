using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acnutech.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class OutParameterToReturnAnalyzer : DiagnosticAnalyzer
    {
        internal static class SingleOutParameterDiagnostic
        {
            internal const string Id = "ACNU0010";

            private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.OutParameterToReturnDiagnostic_Title));
            private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.OutParameterToReturnDiagnostic_MessageFormat));
            private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.OutParameterToReturnDiagnostic_Description));

            private const string Category = "Usage";

            internal static readonly DiagnosticDescriptor Rule
                = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description,
                    helpLinkUri: "https://github.com/acnutech/Analyzers/wiki/ACNU0010");
        }
        internal static class MultipleOutParametersDiagnostic
        {
            internal const string Id = "ACNU0011";

            private static readonly LocalizableString Title = GetLocalizableString(nameof(Resources.OutParametersToTupleDiagnostic_Title));
            private static readonly LocalizableString MessageFormat = GetLocalizableString(nameof(Resources.OutParametersToTupleDiagnostic_MessageFormat));
            private static readonly LocalizableString Description = GetLocalizableString(nameof(Resources.OutParametersToTupleDiagnostic_Description));

            private const string Category = "Usage";

            internal static readonly DiagnosticDescriptor Rule
                = new DiagnosticDescriptor(Id, Title, MessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: Description,
                    helpLinkUri: "https://github.com/acnutech/Analyzers/wiki/ACNU0010");
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(SingleOutParameterDiagnostic.Rule, MultipleOutParametersDiagnostic.Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            MethodDeclarationSyntax method = (MethodDeclarationSyntax)context.Node;

            if (method.ReturnType.ToString() != "void")
            {
                return;
            }

            if (method.Body == null
                || method.Modifiers.Any(SyntaxKind.VirtualKeyword)
                || method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                return;
            }

            if (method.ParameterList.Parameters.Count == 0)
            {
                return;
            }

            var parameterWithOutModifier = ParametersWithOutModifier(method).ToList();
            if (!parameterWithOutModifier.Any())
            {
                return;
            }

            if (parameterWithOutModifier.Any(p => p.AttributeLists.Count > 0))
            {
                return;
            }

            IMethodSymbol methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
            if (methodSymbol.ExplicitInterfaceImplementations.Any() || methodSymbol.ImplementsInterfaceImplicitly())
            {
                return;
            }

            if (parameterWithOutModifier.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(MultipleOutParametersDiagnostic.Rule, method.ReturnType.GetLocation(), methodSymbol.Name));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(SingleOutParameterDiagnostic.Rule, parameterWithOutModifier[0].Modifiers[0].GetLocation(), methodSymbol.Name));
            }
        }

        private static IEnumerable<ParameterSyntax> ParametersWithOutModifier(MethodDeclarationSyntax method) =>
            method.ParameterList.Parameters.Where(
                p =>
                    p.Modifiers.Count == 1
                    && p.Modifiers[0].IsKind(SyntaxKind.OutKeyword));

        private static LocalizableResourceString GetLocalizableString(string name)
            => new LocalizableResourceString(name, Resources.ResourceManager, typeof(Resources));
    }
}
