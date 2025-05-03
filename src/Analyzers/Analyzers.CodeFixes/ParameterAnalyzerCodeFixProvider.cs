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
    public abstract class ParameterAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        protected abstract string Title { get; }
        protected abstract string EquivalenceKey { get; }

        protected ParameterAnalyzerCodeFixProvider()
        {
        }

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
            if (!refModifier.IsKind(FixedModifier))
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

        protected abstract SyntaxKind FixedModifier { get; }

        protected virtual async Task<Solution> UpdateDocument(Solution solution, DocumentId documentId, IEnumerable<Edit> places, UpdateContext updateContext, CancellationToken cancellationToken)
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

        protected virtual UpdateContext CreateContext(ParameterSyntax parameterSyntax, MethodDeclarationSyntax methodDeclarationSyntax, IMethodSymbol methodSymbol)
            => new UpdateContext(methodDeclarationSyntax.ParameterList.Parameters.IndexOf(parameterSyntax));

        protected abstract SyntaxNode GetNodeFromEdit(UpdateContext updateContext, Edit edit, SyntaxNode root);

        protected abstract CSharpSyntaxRewriter CreateSyntaxRewriter(UpdateContext updateContext, ImmutableHashSet<SyntaxNode> nodes);

        protected class UpdateContext
        {
            public int ParameterIndex { get; }

            public UpdateContext(int refParameterIndex)
            {
                ParameterIndex = refParameterIndex;
            }
        }

        [DebuggerDisplay("{GetType().Name} {Span}")]
        protected abstract class Edit
        {
            public readonly TextSpan Span;

            protected Edit(TextSpan textSpan)
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
    }
}
