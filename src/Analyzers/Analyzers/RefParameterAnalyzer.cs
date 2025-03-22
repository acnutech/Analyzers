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
        public const string DiagnosticId = "ACNU0001";

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
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.Parameter);
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            //Debugger.Break();
            // https://github.com/PacktPublishing/Roslyn-Cookbook/blob/master/Chapter03/CodeSamples/Recipe%204%20-%20CodeRefactoringProvider/CodeRefactoring.zip
            ParameterSyntax parameterSyntax = (ParameterSyntax)context.Node;
            if(parameterSyntax.Modifiers.Count != 1)
            {
                return;
            }

            var refModifier = parameterSyntax.Modifiers[0];
            if (refModifier.Kind() != SyntaxKind.RefKeyword)
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

                context.ReportDiagnostic(Diagnostic.Create(Rule, refModifier.GetLocation(), parameterSyntax.Identifier.Text));
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
    }
}
