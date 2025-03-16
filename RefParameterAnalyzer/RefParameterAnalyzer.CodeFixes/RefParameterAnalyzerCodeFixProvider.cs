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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;


namespace Acnutech.RefParameterAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RefParameterAnalyzerCodeFixProvider)), Shared]
    public class RefParameterAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitle,
                    createChangedSolution: c => RemoveRefModifier(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
                diagnostic);
        }

        private async Task<Solution> RemoveRefModifier(Document document, ParameterSyntax parameterSyntax, CancellationToken cancellationToken)
        {
            var otherModifiers = parameterSyntax.Modifiers.Where(m => m.Kind() != SyntaxKind.RefKeyword);
            var newParameter = parameterSyntax.WithModifiers(SyntaxFactory.TokenList(otherModifiers))
                .WithAdditionalAnnotations(Formatter.Annotation);

            SyntaxNode oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode newRoot = oldRoot.ReplaceNode(parameterSyntax, newParameter);

            var methodDeclarationSyntax = parameterSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken);

            var methodReferences = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution, cancellationToken);

            var methodReferencesByDocument =
                methodReferences
                .SelectMany(r => r.Locations)
                .GroupBy(l => l.Document.Id);

            var solution = document.WithSyntaxRoot(newRoot).Project.Solution;

            int refParemeterIndex = methodDeclarationSyntax.ParameterList.Parameters.IndexOf(parameterSyntax);

            foreach (var documentGroup in methodReferencesByDocument)
            {
                var referenceDocument = solution.GetDocument(documentGroup.Key);
                if (referenceDocument == null)
                {
                    continue;
                }

                var documentEditor = await DocumentEditor.CreateAsync(referenceDocument, cancellationToken);

                foreach (var location in documentGroup.OrderByDescending(dg => dg.Location.SourceSpan.Start))
                {
                    var referenceRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken);
                    var methodIdentifierNode = referenceRoot.FindNode(location.Location.SourceSpan);

                    var invocationExpression = methodIdentifierNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();

                    var arguments = invocationExpression.ArgumentList.Arguments;
                    if (arguments == null || arguments.Count <= refParemeterIndex)
                    {
                        continue;
                    }

                    var argument = arguments[refParemeterIndex];
                    if (argument.RefKindKeyword.Kind() != SyntaxKind.RefKeyword)
                    {
                        continue;
                    }

                    var newReferenceRoot = referenceRoot.ReplaceNode(
                        invocationExpression,
                        invocationExpression.WithArgumentList(invocationExpression.ArgumentList.ReplaceNode(argument, argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None)))));

                    documentEditor.ReplaceNode(
                                            invocationExpression,
                                            invocationExpression.WithArgumentList(invocationExpression.ArgumentList.ReplaceNode(argument, argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None)))));
                }

                var updatedDocument = documentEditor.GetChangedDocument();
                solution = updatedDocument.Project.Solution;
            }

            return solution;
        }
    }
}
