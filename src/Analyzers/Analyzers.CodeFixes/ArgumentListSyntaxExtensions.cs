using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Acnutech.Analyzers
{
    internal static class ArgumentListSyntaxExtensions
    {
        public static IEnumerable<SyntaxNodeWithSeparator<ArgumentSyntax>> GetArgumentsWithSeparators(this ArgumentListSyntax argumentListSyntax)
        {
            var lastSeparator = SyntaxFactory.Token(SyntaxKind.CommaToken)
                .WithLeadingTrivia(argumentListSyntax.CloseParenToken.LeadingTrivia);
            return argumentListSyntax.Arguments.GetEachNodeWithFollowingSeparator(lastSeparator);
        }

        public static ArgumentListSyntax WithArguments(this ArgumentListSyntax argumentListSyntax, IEnumerable<SyntaxNodeWithSeparator<ArgumentSyntax>> arguments)
        {
            var argumentList = arguments.ToList();

            var closingParenToken = argumentListSyntax.CloseParenToken;
            if(argumentList.Count > 0)
            {
                closingParenToken = closingParenToken.WithLeadingTrivia(argumentList.Last().Separator.TrailingTrivia);
            }

            return argumentListSyntax
                .WithArguments(arguments.ToSeparatedSyntaxList())
                .WithCloseParenToken(closingParenToken);
        }
    }
}
