using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Acnutech.Analyzers
{
    internal static class SeparatedSyntaxListExtensions
    {
        public static SeparatedSyntaxList<TNode> Without<TNode>(this SeparatedSyntaxList<TNode> list, Func<TNode, bool> predicate)
                where TNode : SyntaxNode
        {
            return SyntaxFactory.SeparatedList<TNode>(SkipNodeIf(list, predicate));
        }

        static IEnumerable<SyntaxNodeOrToken> SkipNodeIf<TNode>(SeparatedSyntaxList<TNode> list, Func<TNode, bool> predicate)
                where TNode : SyntaxNode
        {
            var nodesWithSeps = list.GetWithSeparators();
            bool removeSeparator = false;

            foreach (var item in nodesWithSeps)
            {
                if (item.IsToken)
                {
                    if (!removeSeparator)
                    {
                        yield return item;
                    }

                    removeSeparator = false;
                }
                else if (item.IsNode && item.AsNode() is TNode nodeItem)
                {
                    if (predicate(nodeItem))
                    {
                        removeSeparator = true;
                    }
                    else
                    {
                        yield return item;
                    }

                }
                else
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<SyntaxNodeWithSeparator<T>> GetEachNodeWithFollowingSeparator<T>(this SeparatedSyntaxList<T> list, SyntaxToken trailingSeparator)
            where T : SyntaxNode
        {
            for (int i = 0; i < list.Count; i++)
            {
                var node = list[i];
                var separator = i < list.Count - 1 
                    ? list.GetSeparator(i)
                    : trailingSeparator;
                yield return new SyntaxNodeWithSeparator<T>(node, separator);
            }
        }

        public static SeparatedSyntaxList<T> ToSeparatedSyntaxList<T>(this IEnumerable<SyntaxNodeWithSeparator<T>> nodesWithSeparators)
            where T : SyntaxNode
        {
            var nodes = nodesWithSeparators.ToList();
            var list = new List<SyntaxNodeOrToken>();
            for(int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                list.Add(node.SyntaxNode);
                if (i < nodes.Count - 1)
                {
                    list.Add(node.Separator);
                }
            }
            //foreach (var nodeWithSeparator in nodesWithSeparators)
            //{
            //    list.Add(nodeWithSeparator.SyntaxNode);
            //    if (nodeWithSeparator.Separator != default(SyntaxToken))
            //    {
            //        list.Add(nodeWithSeparator.Separator);
            //    }
            //}
            return SyntaxFactory.SeparatedList<T>(list);
        }
    }

    public class SyntaxNodeWithSeparator<T>
        where T: SyntaxNode
    {
        public SyntaxNodeWithSeparator(T syntaxNode, SyntaxToken separator)
        {
            SyntaxNode = syntaxNode;
            Separator = separator;
        }

        public T SyntaxNode { get; }
        public SyntaxToken Separator { get; }
    }
}
