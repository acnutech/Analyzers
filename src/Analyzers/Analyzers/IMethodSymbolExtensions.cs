using Microsoft.CodeAnalysis;

namespace Acnutech.Analyzers
{
    internal static class IMethodSymbolExtensions
    {
        public static bool ImplementsInterfaceImplicitly(this IMethodSymbol methodSymbol)
        {
            if (methodSymbol.DeclaredAccessibility == Accessibility.Private
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
