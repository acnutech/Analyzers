using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Acnutech.Analyzers
{
    public class UpdateContext
    {
    }

    public abstract class MethodDeclarationCodeFixProvider<TContext, TPrimaryEditNode> : CodeFixProvider
        where TContext : UpdateContext
        where TPrimaryEditNode : SyntaxNode
    {
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        protected abstract string Title { get; }

        protected abstract string EquivalenceKey { get; }

        protected MethodDeclarationCodeFixProvider()
        {
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the node identified by the diagnostic.
            var primaryEditNode = MapSpanToEditNode(root, diagnosticSpan);

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedSolution: c => RewriteMethod(context.Document, primaryEditNode, c),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }

        protected abstract TPrimaryEditNode MapSpanToEditNode(SyntaxNode root, TextSpan span);

        private async Task<Solution> RewriteMethod(Document document, TPrimaryEditNode primaryEditNode, CancellationToken cancellationToken)
        {
            if(TryGetMethodDeclarationSyntax(primaryEditNode, out var methodDeclarationSyntax) == false)
            {
                return document.Project.Solution;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclarationSyntax, cancellationToken);

            var methodReferences = await SymbolFinder.FindReferencesAsync(methodSymbol, document.Project.Solution, cancellationToken);
            var allReferencesByDocument =
                methodReferences
                .SelectMany(r =>
                    r.Locations.Select(l => (DocumentId: l.Document.Id, Edit: (Edit)new ReferencedMethodEdit(l.Location.SourceSpan))))
                .Append((DocumentId: document.Id, Edit: new PrimaryEdit(primaryEditNode.Span)))
                .GroupBy(l => l.DocumentId, l => l.Edit);

            var updateContext = CreateContext(primaryEditNode, methodDeclarationSyntax, methodSymbol);

            var solution = document.Project.Solution;

            foreach (var documentGroup in allReferencesByDocument)
            {
                solution = await UpdateDocument(solution, documentGroup.Key, documentGroup, updateContext, cancellationToken);
            }

            return solution;
        }

        protected abstract bool TryGetMethodDeclarationSyntax(TPrimaryEditNode node, out MethodDeclarationSyntax methodDeclarationSyntax);

        protected virtual async Task<Solution> UpdateDocument(Solution solution, DocumentId documentId, IEnumerable<Edit> places, TContext updateContext, CancellationToken cancellationToken)
        {
            var referenceDocument = solution.GetDocument(documentId);
            if (referenceDocument == null)
            {
                return solution;
            }

            var documentSyntaxRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken);
            ImmutableHashSet<SyntaxNode> nodes = places
                .Select(place => GetNodeFromEdit(updateContext, place, documentSyntaxRoot))
                .ToImmutableHashSet();

            var walker = CreateSyntaxRewriter(updateContext, nodes);
            var updatedDocument = walker.Visit(documentSyntaxRoot);

            if (updatedDocument == documentSyntaxRoot)
            {
                return solution;
            }

            var formattedDocument = await Formatter.FormatAsync(referenceDocument.WithSyntaxRoot(updatedDocument), Formatter.Annotation, cancellationToken: cancellationToken);
            return formattedDocument.Project.Solution;
        }

        protected abstract TContext CreateContext(TPrimaryEditNode primaryEditNode, MethodDeclarationSyntax methodDeclarationSyntax, IMethodSymbol methodSymbol);

        protected abstract SyntaxNode GetNodeFromEdit(TContext updateContext, Edit edit, SyntaxNode root);

        protected abstract CSharpSyntaxRewriter CreateSyntaxRewriter(TContext updateContext, ImmutableHashSet<SyntaxNode> nodes);


        [DebuggerDisplay("{GetType().Name} {Span}")]
        protected abstract class Edit
        {
            public readonly TextSpan Span;

            protected Edit(TextSpan textSpan)
            {
                Span = textSpan;
            }
        }

        protected class PrimaryEdit : Edit
        {
            public PrimaryEdit(TextSpan textSpan) : base(textSpan)
            {
            }
        }

        protected class ReferencedMethodEdit : Edit
        {
            public ReferencedMethodEdit(TextSpan textSpan) : base(textSpan)
            {
            }
        }
    }
}
