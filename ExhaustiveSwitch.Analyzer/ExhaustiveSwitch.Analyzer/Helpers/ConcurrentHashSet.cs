using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ExhaustiveSwitch.Analyzer
{
    /// <summary>
    /// スレッドセーフなハッシュセットの簡易実装
    /// </summary>
    internal class ConcurrentHashSet<T> : IEnumerable<T>
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary;

        // コンストラクタ
        public ConcurrentHashSet()
        {
            _dictionary = new ConcurrentDictionary<T, byte>();
        }

        // EqualityComparerを指定する場合のコンストラクタ
        public ConcurrentHashSet(IEqualityComparer<T> comparer)
        {
            _dictionary = new ConcurrentDictionary<T, byte>(comparer);
        }

        // 追加 (成功すれば true, 既に存在すれば false)
        public bool Add(T item)
        {
            // 値(byte)には意味がないので0を入れる
            return _dictionary.TryAdd(item, 0);
        }

        // 削除 (成功すれば true, 存在しなければ false)
        public bool Remove(T item)
        {
            return _dictionary.TryRemove(item, out _);
        }

        // 存在確認
        public bool Contains(T item)
        {
            return _dictionary.ContainsKey(item);
        }

        // 全クリア
        public void Clear()
        {
            _dictionary.Clear();
        }

        // 要素数
        public int Count => _dictionary.Count;

        // 列挙 (スレッドセーフだが、列挙中の変更は反映される場合とされない場合がある)
        public IEnumerator<T> GetEnumerator()
        {
            return _dictionary.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public HashSet<T> ToHashSet()
        {
            return new HashSet<T>(_dictionary.Keys);
        }
    }
}