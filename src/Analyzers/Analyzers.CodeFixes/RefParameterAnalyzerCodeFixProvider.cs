using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.CodeAnalysis.Text;

namespace Acnutech.Analyzers
{
    public abstract class RefParameterAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        protected abstract string Title { get; }
        protected abstract string EquivalenceKey { get; }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ParameterSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedSolution: c => RemoveRefModifier(context.Document, declaration, c),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }

        private async Task<Solution> RemoveRefModifier(Document document, ParameterSyntax parameterSyntax, CancellationToken cancellationToken)
        {
            if (parameterSyntax.Modifiers.Count != 1)
            {
                return document.Project.Solution;
            }

            var refModifier = parameterSyntax.Modifiers[0];
            if (!refModifier.IsKind(SyntaxKind.RefKeyword))
            {
                return document.Project.Solution;
            }

            var methodDeclarationSyntax = parameterSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken);

            var methodReferences = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution, cancellationToken);
            var allReferencesByDocument =
                methodReferences
                .SelectMany(r =>
                    r.Locations.Select(l => (DocumentId: l.Document.Id, Edit: (Edit)new ReferencedMethodEdit(l.Location.SourceSpan))))
                .Append((DocumentId: document.Id, Edit: new ParameterEdit(parameterSyntax.Span)))
                .GroupBy(l => l.DocumentId, l => l.Edit);

            var updateContext = CreateContext(parameterSyntax, methodDeclarationSyntax, methodSymbol);

            var solution = document.Project.Solution;

            foreach (var documentGroup in allReferencesByDocument)
            {
                solution = await UpdateDocument(solution, documentGroup.Key, documentGroup, updateContext, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> UpdateDocument(
            Solution solution,
            DocumentId documentId,
            IEnumerable<Edit> places,
            UpdateContext updateContext,
            CancellationToken cancellationToken)
        {
            var referenceDocument = solution.GetDocument(documentId);
            if (referenceDocument == null)
            {
                return solution;
            }

            var documentEditor = await DocumentEditor.CreateAsync(referenceDocument, cancellationToken);
            var documentSyntaxRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken);

            var orderedEdits = TransformUpdatePlaces(updateContext, documentSyntaxRoot, places)
                .OrderByDescending(sp => sp.Span.Start);

            foreach (var edit in orderedEdits)
            {
                var node = documentSyntaxRoot.FindNode(edit.Span);

                PerformEdit(updateContext, documentEditor, edit, node);
            }

            var updatedDocument = documentEditor.GetChangedDocument();
            var formattedDocument = await Formatter.FormatAsync(updatedDocument, Formatter.Annotation, cancellationToken: cancellationToken);
            return formattedDocument.Project.Solution;
        }

        private void PerformEdit(UpdateContext updateContext, DocumentEditor documentEditor, Edit edit, SyntaxNode node)
        {
            if (edit is ParameterEdit)
            {
                UpdateRefParameter(documentEditor, node);
            }
            else
            {
                UpdateRefArgument(documentEditor, node);
            }
        }

        protected virtual UpdateContext CreateContext(ParameterSyntax parameterSyntax, MethodDeclarationSyntax methodDeclarationSyntax, IMethodSymbol methodSymbol)
            => new UpdateContext(methodDeclarationSyntax.ParameterList.Parameters.IndexOf(parameterSyntax));

        protected virtual IEnumerable<Edit> TransformUpdatePlaces(UpdateContext context, SyntaxNode documentRootNode, IEnumerable<Edit> edits)
        {
            foreach (var edit in edits)
            {
                switch (edit)
                {
                    case ParameterEdit parameterUpdatePlace:
                        yield return parameterUpdatePlace;
                        break;
                    case ReferencedMethodEdit referenceMethodUpdatePlace:
                        var node = documentRootNode.FindNode(referenceMethodUpdatePlace.Span);
                        var invocationExpression = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                        if (invocationExpression != null)
                        {
                            var arguments = invocationExpression.ArgumentList.Arguments;
                            if (arguments.Count > context.RefParameterIndex)
                            {
                                yield return new ArgumentEdit(arguments[context.RefParameterIndex].Span);
                            }
                        }
                        break;
                }
            }
        }

        protected abstract void UpdateRefParameter(DocumentEditor documentEditor, SyntaxNode node);

        protected abstract void UpdateRefArgument(DocumentEditor documentEditor, SyntaxNode node);

        protected class UpdateContext
        {
            public int RefParameterIndex { get; }

            public UpdateContext(int refParameterIndex)
            {
                RefParameterIndex = refParameterIndex;
            }
        }

        [DebuggerDisplay("{GetType().Name} {Span}")]
        protected abstract class Edit
        {
            public readonly TextSpan Span;

            public Edit(TextSpan textSpan)
            {
                Span = textSpan;
            }
        }

        protected class ParameterEdit : Edit
        {
            public ParameterEdit(TextSpan textSpan) : base(textSpan)
            {
            }
        }

        protected class ReferencedMethodEdit : Edit
        {
            public ReferencedMethodEdit(TextSpan textSpan) : base(textSpan)
            {
            }
        }

        protected class ArgumentEdit : Edit
        {
            public ArgumentEdit(TextSpan textSpan) : base(textSpan)
            {
            }
        }
    }
}
