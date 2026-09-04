using System.Collections;
using System.Collections.Generic;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.OrderedDictionary
{
    public interface IReadOnlyOrderedDictionary<TKey, TValue>
    {
        public TValue this[TKey key] { get; }

        public int Count { get; }

        public bool ContainsKey(TKey key);

        public bool TryGetValue(TKey key, out TValue value);

        public void Remove(TKey key);

        public void Clear();

        public IEnumerable<TKey> Keys { get; }

        public IEnumerable<TValue> Values { get; }
    }

    public class OrderedDictionary<TKey, TValue> : IReadOnlyOrderedDictionary<TKey, TValue>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();

        private readonly List<TKey> _order = new();

        public void Add(TKey key, TValue value)
        {
            if (!_dictionary.TryAdd(key, value))
            {
                return;
            }

            _order.Add(key);
        }

        public void Prepend(TKey key, TValue value)
        {
            if (!_dictionary.TryAdd(key, value))
            {
                return;
            }

            _order.Insert(0, key);
        }

        public TValue this[TKey key] => _dictionary[key];

        public int Count => _dictionary.Count;

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            if (!ContainsKey(key))
            {
                return;
            }

            _dictionary.Remove(key);

            _order.Remove(key);
        }

        public void Clear()
        {
            _dictionary.Clear();

            _order.Clear();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public IEnumerable<TKey> Keys => _order;

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var key in _order)
                {
                    yield return _dictionary[key];
                }
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in _order)
            {
                yield return new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public TKey GetKeyByIndex(int index)
        {
            return _order[index];
        }
    }
}