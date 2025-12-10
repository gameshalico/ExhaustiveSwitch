using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    public class ExhaustiveHierarchyInfo
    {
        // 全ての対象型（O(1)検索用）
        public HashSet<INamedTypeSymbol> AllCases { get; }
        
        // 親 -> 子のリスト（ある親を実装・継承している直下の子たち）
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectChildrenMap { get; }
    
        // 子 -> 親のリスト（多重継承・インターフェース対応のためList）
        public Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> DirectParentsMap { get; }
    
        public ExhaustiveHierarchyInfo(HashSet<INamedTypeSymbol> allCases)
        {
            AllCases = allCases;
            DirectChildrenMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
            DirectParentsMap = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
    
            foreach (var type in allCases)
            {
                // この型の「Exhaustiveグラフ上の直接の親」をすべて見つける
                var parents = FindDirectParents(type, allCases);
    
                // マップ構築
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
            
            // 探索済みチェック用（ダイヤモンド継承などの重複防止）
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<INamedTypeSymbol>();
    
            // 探索開始：BaseType
            if (type.BaseType != null) queue.Enqueue(type.BaseType);
            
            // 探索開始：Interfaces
            foreach (var iface in type.Interfaces)
            {
                queue.Enqueue(iface);
            }
    
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
    
                if (!visited.Add(current)) continue;
    
                // ヒットした -> これ以上奥（親の親）は探さない（直近の親が担当するため）
                if (allCases.Contains(current))
                {
                    results.Add(current);
                }
                else
                {
                    // ヒットしなかった（Case属性のない中間クラス/インターフェース）
                    // -> さらに親をキューに入れる
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