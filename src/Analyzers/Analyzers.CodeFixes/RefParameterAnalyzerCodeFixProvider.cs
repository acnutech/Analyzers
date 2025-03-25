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
using Microsoft.CodeAnalysis.Text;


namespace Acnutech.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RefParameterAnalyzerCodeFixProvider)), Shared]
    public class RefParameterAnalyzerCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(RefParameterAnalyzer.RemoveUnnecessaryRefModifierDiagnostic.Id); }
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

        private static async Task<Solution> UpdateDocument(
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

        private static void UpdateRefParameter(DocumentEditor documentEditor, SyntaxNode node)
        {
            var parameterSyntax1 = (ParameterSyntax)node;

            var refModifier1 = parameterSyntax1.Modifiers[0];
            var parameterTrivia = GetTriviaAfterNodeRemoval(refModifier1, parameterSyntax1.Type.GetLeadingTrivia());

            var newParameter = parameterSyntax1.WithModifiers(SyntaxFactory.TokenList())
                .WithType(parameterSyntax1.Type.WithLeadingTrivia(parameterTrivia))
                .WithAdditionalAnnotations(Formatter.Annotation);

            documentEditor.ReplaceNode(node, newParameter);
        }

        private static void UpdateRefArgument(DocumentEditor documentEditor, SyntaxNode node, int refParameterIndex)
        {
            var invocationExpression = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count <= refParameterIndex)
            {
                return;
            }

            var argument = arguments[refParameterIndex];

            if (!argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                || !(argument.Expression is IdentifierNameSyntax identifier))
            {
                return;
            }

            var trivia = GetTriviaAfterNodeRemoval(argument.RefKindKeyword, identifier.Identifier.LeadingTrivia);

            var argumentWithoutRef =
                argument
                    .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.None))
                    .WithLeadingTrivia(trivia)
                    .WithAdditionalAnnotations(Formatter.Annotation);

            documentEditor.ReplaceNode(
                invocationExpression,
                invocationExpression.WithArgumentList(
                    invocationExpression.ArgumentList.ReplaceNode(
                        argument,
                        argumentWithoutRef)));
        }

        private static SyntaxTriviaList GetTriviaAfterNodeRemoval(SyntaxToken removedNode, SyntaxTriviaList nextLeadingTrivia)
            => removedNode.LeadingTrivia
                .AddRange(NonTrivialTrivia(removedNode.TrailingTrivia))
                .AddRange(nextLeadingTrivia);

        private static SyntaxTriviaList NonTrivialTrivia(SyntaxTriviaList trivia)
            => trivia.Count == 1
                && trivia[0].IsKind(SyntaxKind.WhitespaceTrivia)
                && trivia[0].ToString() == " "
                ? SyntaxFactory.TriviaList()
                : trivia;

        private readonly struct RefParameterInfo
        {
            public RefParameterInfo(DocumentId documentId, int parameterSyntaxSpanStart , int refParameterIndex)
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
