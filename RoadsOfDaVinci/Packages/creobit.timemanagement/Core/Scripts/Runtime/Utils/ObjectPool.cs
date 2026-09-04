using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public class ObjectPool<T> : IDisposable, IObjectPool<T> where T : class
    {
        private readonly List<T> _list;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _actionOnGet;
        private readonly Action<T> _actionOnRelease;
        private readonly Action<T> _actionOnDestroy;
        private readonly int _maxSize;
        private readonly bool _collectionCheck;

        public int CountAll { get; private set; }

        public int CountActive => CountAll - CountInactive;

        public int CountInactive => _list.Count;

        public ObjectPool(
          Func<T> createFunc,
          Action<T> actionOnGet = null,
          Action<T> actionOnRelease = null,
          Action<T> actionOnDestroy = null,
          bool collectionCheck = true,
          int defaultCapacity = 10,
          int maxSize = 10000)
        {
            if (maxSize <= 0)
                throw new ArgumentException("Max Size must be greater than 0", nameof(maxSize));
            _list = new List<T>(defaultCapacity);
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _maxSize = maxSize;
            _actionOnGet = actionOnGet;
            _actionOnRelease = actionOnRelease;
            _actionOnDestroy = actionOnDestroy;
            _collectionCheck = collectionCheck;
        }

        public T Get()
        {
            T obj;
            if (_list.Count == 0)
            {
                obj = _createFunc();
                ++CountAll;
            }
            else
            {
                obj = _list[0];
                _list.RemoveAt(0);
            }

            _actionOnGet?.Invoke(obj);

            return obj;
        }

        public PooledObject<T> Get(out T v)
        {
            return new PooledObject<T>(v = Get(), this);
        }

        public void ReleaseLast()
        {
            T lastElement = _list.Count > 0 ? _list[_list.Count - 1] : null;

            if (lastElement == null)
            {
                return;
            }

            Release(lastElement);
        }

        public void Release(T element)
        {
            if (_collectionCheck && _list.Count > 0)
            {
                for (var index = 0; index < _list.Count; ++index)
                {
                    if (element == _list[index])
                        throw new InvalidOperationException("Trying to release an object that has already been released to the pool.");
                }
            }

            _actionOnRelease?.Invoke(element);

            if (CountInactive < _maxSize)
            {
                _list.Add(element);
            }
            else
            {
                --CountAll;

                _actionOnDestroy?.Invoke(element);
            }
        }

        public void Clear()
        {
            foreach (var obj in _list)
            {
                _actionOnDestroy?.Invoke(obj);

            }

            CountAll = 0;
            _list.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}