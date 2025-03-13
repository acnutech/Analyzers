using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RefParameterAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RefParameterAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "RefParameterAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            //context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.Parameter);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            //Debugger.Break();
            // https://github.com/PacktPublishing/Roslyn-Cookbook/blob/master/Chapter03/CodeSamples/Recipe%204%20-%20CodeRefactoringProvider/CodeRefactoring.zip
            ParameterSyntax parameterSyntax = (ParameterSyntax)context.Node;
            var pos = parameterSyntax.Modifiers.IndexOf(SyntaxKind.RefKeyword);
            if (pos < 0)
            {
                return;
            }

            try
            {
                // Perform data flow analysis on the parameter.
                var method = parameterSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();

                if (method.Body == null)
                {
                    return;
                }

                if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    return;
                }

                IMethodSymbol methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
                if (methodSymbol.ExplicitInterfaceImplementations.Any() || ImplementsInterfaceImplicitly(methodSymbol))
                {
                    return;
                }

                DataFlowAnalysis dataFlowAnalysis = context.SemanticModel.AnalyzeDataFlow(method.Body);

                ISymbol parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken);
                if (dataFlowAnalysis.WrittenInside.Contains(parameterSymbol))
                {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(Rule, parameterSyntax.Modifiers[pos].GetLocation(), parameterSyntax.Identifier.Text));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        private bool ImplementsInterfaceImplicitly(IMethodSymbol methodSymbol)
        {
            if(methodSymbol.DeclaredAccessibility == Accessibility.Private
                || methodSymbol.DeclaredAccessibility == Accessibility.NotApplicable)
            {
                return false;
            }

            var containingType = methodSymbol.ContainingType;
            foreach (var interfaceType in containingType.AllInterfaces)
            {
                foreach (var interfaceMember in interfaceType.GetMembers())
                {
                    if (SymbolEqualityComparer.Default.Equals(methodSymbol, containingType.FindImplementationForInterfaceMember(interfaceMember)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            return;
            //Debugger.Break();
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
