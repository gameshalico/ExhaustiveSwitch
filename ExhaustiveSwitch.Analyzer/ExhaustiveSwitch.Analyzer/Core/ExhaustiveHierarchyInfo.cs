using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    public class ExhaustiveHierarchyInfo
    {
        public HashSet<INamedTypeSymbol> AllCases { get; }
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectChildrenMap { get; }
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectParentsMap { get; }

        /// <summary>
        /// Exhaustive型がジェネリック型かどうか
        /// </summary>
        public bool IsGeneric { get; }

        public ExhaustiveHierarchyInfo(HashSet<INamedTypeSymbol> allCases)
        {
            AllCases = allCases;
            DirectChildrenMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            DirectParentsMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            // ジェネリック型かどうかを判定（いずれかのCase型がジェネリックならtrue）
            IsGeneric = allCases.Any(t => t.IsGenericType);

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
        /// ジェネリック型の場合、型引数を適用してspecializedな型のセットを生成します
        /// </summary>
        public ExhaustiveHierarchyInfo ApplyTypeArguments(INamedTypeSymbol constructedExhaustiveType)
        {
            if (!IsGeneric || !constructedExhaustiveType.IsGenericType)
            {
                return this;
            }

            var typeArguments = constructedExhaustiveType.TypeArguments;
            var constructedCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var caseType in AllCases)
            {
                if (caseType.IsGenericType && caseType.TypeArguments.Length == typeArguments.Length)
                {
                    // 型引数を適用して構築型を作成
                    var constructedCase = caseType.OriginalDefinition.Construct(typeArguments.ToArray());
                    constructedCases.Add(constructedCase);
                }
                else
                {
                    // 非ジェネリック型の場合はそのまま追加
                    constructedCases.Add(caseType);
                }
            }

            return new ExhaustiveHierarchyInfo(constructedCases);
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