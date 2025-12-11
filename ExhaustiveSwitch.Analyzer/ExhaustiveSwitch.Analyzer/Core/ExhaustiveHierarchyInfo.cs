using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    public class ExhaustiveHierarchyInfo
    {
        public HashSet<INamedTypeSymbol> AllCases { get; }
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectChildrenMap { get; }
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectParentsMap { get; }
    
        public ExhaustiveHierarchyInfo(HashSet<INamedTypeSymbol> allCases)
        {
            AllCases = allCases;
            DirectChildrenMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            DirectParentsMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
    
            foreach (var type in allCases)
            {
                var parents = FindDirectParents(type, allCases);

                if (parents.Count > 0)
                {
                    DirectParentsMap[type] = parents;
    
                    foreach (var parent in parents)
                    {
                        if (!DirectChildrenMap.TryGetValue(parent, out var list))
                        {
                            list = new List<INamedTypeSymbol>();
                            DirectChildrenMap[parent] = list;
                        }
                        list.Add(type);
                    }
                }
            }
        }
    
        /// <summary>
        /// 継承ツリー/インターフェース列を遡り、AllCasesに含まれる「最も近い祖先」を探す
        /// </summary>
        private List<INamedTypeSymbol> FindDirectParents(INamedTypeSymbol type, HashSet<INamedTypeSymbol> allCases)
        {
            var results = new List<INamedTypeSymbol>();
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<INamedTypeSymbol>();

            if (type.BaseType != null)
            {
                queue.Enqueue(type.BaseType);
            }

            foreach (var iface in type.Interfaces)
            {
                queue.Enqueue(iface);
            }
    
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
    
                if (!visited.Add(current))
                {
                    continue;
                }

                if (allCases.Contains(current))
                {
                    results.Add(current);
                }
                else
                {
                    if (current.BaseType != null)
                    {
                        queue.Enqueue(current.BaseType);
                    }
                    foreach (var iface in current.Interfaces)
                    {
                        queue.Enqueue(iface);
                    }
                }
            }
    
            return results;
        }
    }
}