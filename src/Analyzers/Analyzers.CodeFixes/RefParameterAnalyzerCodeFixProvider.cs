using System.Collections.Generic;
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
            var diagnostic = context.Diagnostics.First();
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
                .SelectMany(r => r.Locations.Select(l => (DocumentId: l.Document.Id, l.Location.SourceSpan)))
                .Append((DocumentId: document.Id, SourceSpan: parameterSyntax.Span))
                .GroupBy(l => l.DocumentId, l => l.SourceSpan);

            var refParameterInfo = new RefParameterInfo(
                document.Id,
                parameterSyntax.Span.Start,
                methodDeclarationSyntax.ParameterList.Parameters.IndexOf(parameterSyntax));

            var solution = document.Project.Solution;

            foreach (var documentGroup in allReferencesByDocument)
            {
                solution = await UpdateDocument(solution, documentGroup.Key, documentGroup.Select(dg => dg), refParameterInfo, cancellationToken);
            }

            return solution;
        }

        private async Task<Solution> UpdateDocument(
            Solution solution,
            DocumentId documentId,
            IEnumerable<TextSpan> spans,
            RefParameterInfo refParameterInfo,
            CancellationToken cancellationToken)
        {
            var referenceDocument = solution.GetDocument(documentId);
            if (referenceDocument == null)
            {
                return solution;
            }

            var documentEditor = await DocumentEditor.CreateAsync(referenceDocument, cancellationToken);
            var documentSyntaxRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken);

            var spansInEditOrder = spans
                .OrderByDescending(sp => sp.Start);

            foreach (var span in spansInEditOrder)
            {
                var node = documentSyntaxRoot.FindNode(span);

                if (referenceDocument.Id == refParameterInfo.DocumentId && span.Start == refParameterInfo.ParameterSyntaxSpanStart)
                {
                    UpdateRefParameter(documentEditor, node);
                }
                else
                {
                    UpdateRefArgument(documentEditor, node, refParameterInfo.RefParameterIndex);
                }
            }

            var updatedDocument1 = documentEditor.GetChangedDocument();
            var formattedDocument = await Formatter.FormatAsync(updatedDocument1, Formatter.Annotation, cancellationToken: cancellationToken);
            return formattedDocument.Project.Solution;
        }

        protected abstract void UpdateRefParameter(DocumentEditor documentEditor, SyntaxNode node);

        protected abstract void UpdateRefArgument(DocumentEditor documentEditor, SyntaxNode node, int refParameterIndex);

        private readonly struct RefParameterInfo
        {
            public RefParameterInfo(DocumentId documentId, int parameterSyntaxSpanStart, int refParameterIndex)
            {
                DocumentId = documentId;
                ParameterSyntaxSpanStart = parameterSyntaxSpanStart;
                RefParameterIndex = refParameterIndex;
            }

            public DocumentId DocumentId { get; }
            public int ParameterSyntaxSpanStart { get; }
            public int RefParameterIndex { get; }
        }
    }
}
