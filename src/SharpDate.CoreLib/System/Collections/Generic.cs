using System.Runtime.CompilerServices;

namespace System.Collections
{
    public interface IEnumerable
    {
        IEnumerator GetEnumerator();
    }

    public interface IEnumerator
    {
        object Current { get; }
        bool MoveNext();
        void Reset();
    }

    public interface ICollection : IEnumerable
    {
        int Count { get; }
    }

    public interface IList : ICollection
    {
        object this[int index] { get; set; }
    }
}

namespace System.Collections.Generic
{
    public interface IEnumerable<out T> : IEnumerable
    {
        new IEnumerator<T> GetEnumerator();
    }

    public interface IEnumerator<out T> : IDisposable, IEnumerator
    {
        new T Current { get; }
    }

    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        int Count { get; }
    }

    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[int index] { get; }
    }

    public interface ICollection<T> : IEnumerable<T>
    {
        int Count { get; }
        bool IsReadOnly { get; }
        void Add(T item);
        void Clear();
        bool Contains(T item);
        void CopyTo(T[] array, int arrayIndex);
        bool Remove(T item);
    }

    public interface IList<T> : ICollection<T>
    {
        T this[int index] { get; set; }
        int IndexOf(T item);
        void Insert(int index, T item);
        void RemoveAt(int index);
    }

    public interface IReadOnlyDictionary<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        TValue this[TKey key] { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, out TValue value);
    }

    public interface IEqualityComparer<in T>
    {
        bool Equals(T x, T y);
        int GetHashCode(T obj);
    }

    public interface IComparer<in T>
    {
        int Compare(T x, T y);
    }

    public abstract class EqualityComparer<T> : IEqualityComparer<T>
    {
        public static EqualityComparer<T> Default { get; } = new DefaultEqualityComparer<T>();

        public abstract bool Equals(T x, T y);
        public abstract int GetHashCode(T obj);
    }

    internal sealed class DefaultEqualityComparer<T> : EqualityComparer<T>
    {
        public override bool Equals(T x, T y)
        {
            if (x is null)
                return y is null;
            if (y is null)
                return false;
            return x.Equals(y);
        }

        public override int GetHashCode(T obj) => obj is null ? 0 : obj.GetHashCode();
    }

    public abstract class Comparer<T> : IComparer<T>
    {
        public static Comparer<T> Default { get; } = new DefaultComparer<T>();

        public static Comparer<T> Create(Comparison<T> comparison) => new ComparisonComparer<T>(comparison);

        public abstract int Compare(T x, T y);
    }

    internal sealed class DefaultComparer<T> : Comparer<T>
    {
        public override int Compare(T x, T y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;
            if (x is IComparable<T> c)
                return c.CompareTo(y);
            ThrowHelpers.ThrowInvalidOperationException("Type does not implement IComparable<T>.");
            return 0;
        }
    }

    internal sealed class ComparisonComparer<T> : Comparer<T>
    {
        private readonly Comparison<T> _comparison;
        public ComparisonComparer(Comparison<T> comparison) { _comparison = comparison; }
        public override int Compare(T x, T y) => _comparison(x, y);
    }

    public readonly struct KeyValuePair<TKey, TValue>
    {
        public KeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public TKey Key { get; }
        public TValue Value { get; }

        public void Deconstruct(out TKey key, out TValue value)
        {
            key = Key;
            value = Value;
        }
    }

    public class List<T> : IList<T>, IReadOnlyList<T>
    {
        private T[] _items;
        private int _size;
        private int _version;

        public List() { _items = Array.Empty<T>(); }

        public List(int capacity)
        {
            if (capacity < 0)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        }

        public List(IEnumerable<T> collection) : this()
        {
            foreach (T item in collection)
                Add(item);
        }

        public int Count => _size;

        public bool IsReadOnly => false;

        public int Capacity
        {
            get => _items.Length;
            set
            {
                if (value < _size)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                if (value != _items.Length)
                {
                    T[] newItems = new T[value];
                    Array.Copy(_items, newItems, _size);
                    _items = newItems;
                }
            }
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_size)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                return _items[index];
            }
            set
            {
                if ((uint)index >= (uint)_size)
                    ThrowHelpers.ThrowArgumentOutOfRangeException();
                _items[index] = value;
                _version++;
            }
        }

        public void Add(T item)
        {
            if (_size == _items.Length)
                Grow(_size + 1);
            _items[_size++] = item;
            _version++;
        }

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (T item in collection)
                Add(item);
        }

        public void AddRange(ReadOnlySpan<T> items)
        {
            if (_size + items.Length > _items.Length)
                Grow(_size + items.Length);
            for (int i = 0; i < items.Length; i++)
                _items[_size++] = items[i];
            _version++;
        }

        private void Grow(int min)
        {
            int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
            if (newCapacity < min)
                newCapacity = min;
            Capacity = newCapacity;
        }

        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(_items, 0, _size);
            _size = 0;
            _version++;
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public int IndexOf(T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _size; i++)
                if (comparer.Equals(_items[i], item))
                    return i;
            return -1;
        }

        public int FindIndex(Predicate<T> match)
        {
            for (int i = 0; i < _size; i++)
                if (match(_items[i]))
                    return i;
            return -1;
        }

        public T Find(Predicate<T> match)
        {
            for (int i = 0; i < _size; i++)
                if (match(_items[i]))
                    return _items[i];
            return default;
        }

        public bool Exists(Predicate<T> match) => FindIndex(match) >= 0;

        public void Insert(int index, T item)
        {
            if ((uint)index > (uint)_size)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            if (_size == _items.Length)
                Grow(_size + 1);
            if (index < _size)
                Array.Copy(_items, index, _items, index + 1, _size - index);
            _items[index] = item;
            _size++;
            _version++;
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0)
                return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_size)
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            _size--;
            if (index < _size)
                Array.Copy(_items, index + 1, _items, index, _size - index);
            _items[_size] = default;
            _version++;
        }

        public void RemoveRange(int index, int count)
        {
            if ((uint)index > (uint)_size || (uint)count > (uint)(_size - index))
                ThrowHelpers.ThrowArgumentOutOfRangeException();
            if (count == 0)
                return;
            _size -= count;
            if (index < _size)
                Array.Copy(_items, index + count, _items, index, _size - index);
            for (int i = _size; i < _size + count; i++)
                _items[i] = default;
            _version++;
        }

        public int RemoveAll(Predicate<T> match)
        {
            int free = 0;
            for (int i = 0; i < _size; i++)
            {
                if (!match(_items[i]))
                    _items[free++] = _items[i];
            }
            int removed = _size - free;
            for (int i = free; i < _size; i++)
                _items[i] = default;
            _size = free;
            _version++;
            return removed;
        }

        public void Reverse()
        {
            int i = 0, j = _size - 1;
            while (i < j)
            {
                T tmp = _items[i];
                _items[i] = _items[j];
                _items[j] = tmp;
                i++;
                j--;
            }
            _version++;
        }

        public void Sort(Comparison<T> comparison)
        {
            Array.Sort(_items, 0, _size, comparison);
            _version++;
        }

        public void Sort() => Sort(Comparer<T>.Default.Compare);

        public void ForEach(Action<T> action)
        {
            for (int i = 0; i < _size; i++)
                action(_items[i]);
        }

        public void CopyTo(T[] array, int arrayIndex) => Array.Copy(_items, 0, array, arrayIndex, _size);

        public T[] ToArray()
        {
            T[] result = new T[_size];
            Array.Copy(_items, result, _size);
            return result;
        }

        public Span<T> AsSpan() => new Span<T>(_items, 0, _size);

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<T>
        {
            private readonly List<T> _list;
            private readonly int _version;
            private int _index;
            private T _current;

            internal Enumerator(List<T> list)
            {
                _list = list;
                _version = list._version;
                _index = 0;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_version != _list._version)
                    ThrowHelpers.ThrowInvalidOperationException("Collection was modified; enumeration operation may not execute.");
                if ((uint)_index < (uint)_list._size)
                {
                    _current = _list._items[_index];
                    _index++;
                    return true;
                }
                _current = default;
                _index = _list._size + 1;
                return false;
            }

            public T Current => _current;
            object IEnumerator.Current => _current;
            public void Reset() { _index = 0; _current = default; }
            public void Dispose() { }
        }
    }

    public class Dictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private struct Entry
        {
            public uint HashCode;
            public int Next; // index of next entry in the bucket chain, -1 for end; for free entries: next free
            public TKey Key;
            public TValue Value;
        }

        private int[] _buckets; // 1-based index into _entries, 0 = empty
        private Entry[] _entries;
        private int _count;
        private int _freeList = -1;
        private int _freeCount;
        private int _version;
        private readonly IEqualityComparer<TKey> _comparer;

        public Dictionary() : this(0, null) { }
        public Dictionary(int capacity) : this(capacity, null) { }
        public Dictionary(IEqualityComparer<TKey> comparer) : this(0, comparer) { }

        public Dictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity > 0)
                Initialize(capacity);
            _comparer = comparer;
        }

        public int Count => _count - _freeCount;

        private void Initialize(int capacity)
        {
            int size = 4;
            while (size < capacity)
                size *= 2;
            _buckets = new int[size];
            _entries = new Entry[size];
        }

        private uint Hash(TKey key) => (uint)(_comparer != null ? _comparer.GetHashCode(key) : key.GetHashCode());

        private bool KeyEquals(TKey a, TKey b) => _comparer != null ? _comparer.Equals(a, b) : EqualityComparer<TKey>.Default.Equals(a, b);

        private int FindEntry(TKey key)
        {
            if (key is null)
                ThrowHelpers.ThrowArgumentNullException();
            if (_buckets == null)
                return -1;
            uint hash = Hash(key);
            int i = _buckets[hash & (uint)(_buckets.Length - 1)] - 1;
            while (i >= 0)
            {
                ref Entry e = ref _entries[i];
                if (e.HashCode == hash && KeyEquals(e.Key, key))
                    return i;
                i = e.Next;
            }
            return -1;
        }

        public TValue this[TKey key]
        {
            get
            {
                int i = FindEntry(key);
                if (i < 0)
                    ThrowHelpers.ThrowKeyNotFoundException();
                return _entries[i].Value;
            }
            set => Insert(key, value, overwrite: true);
        }

        public void Add(TKey key, TValue value)
        {
            if (!Insert(key, value, overwrite: false))
                ThrowHelpers.ThrowArgumentException();
        }

        public bool TryAdd(TKey key, TValue value) => Insert(key, value, overwrite: false);

        private bool Insert(TKey key, TValue value, bool overwrite)
        {
            if (key is null)
                ThrowHelpers.ThrowArgumentNullException();
            if (_buckets == null)
                Initialize(4);
            uint hash = Hash(key);
            ref int bucket = ref _buckets[hash & (uint)(_buckets.Length - 1)];
            for (int i = bucket - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hash && KeyEquals(_entries[i].Key, key))
                {
                    if (!overwrite)
                        return false;
                    _entries[i].Value = value;
                    _version++;
                    return true;
                }
            }

            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                _freeList = -3 - _entries[index].Next;
                _freeCount--;
            }
            else
            {
                if (_count == _entries.Length)
                {
                    Resize();
                    bucket = ref _buckets[hash & (uint)(_buckets.Length - 1)];
                }
                index = _count++;
            }

            ref Entry entry = ref _entries[index];
            entry.HashCode = hash;
            entry.Next = bucket - 1;
            entry.Key = key;
            entry.Value = value;
            bucket = index + 1;
            _version++;
            return true;
        }

        private void Resize()
        {
            int newSize = _entries.Length * 2;
            Entry[] entries = new Entry[newSize];
            Array.Copy(_entries, entries, _count);
            int[] buckets = new int[newSize];
            for (int i = 0; i < _count; i++)
            {
                if (entries[i].Next >= -1)
                {
                    ref int b = ref buckets[entries[i].HashCode & (uint)(newSize - 1)];
                    entries[i].Next = b - 1;
                    b = i + 1;
                }
            }
            _buckets = buckets;
            _entries = entries;
        }

        public bool ContainsKey(TKey key) => FindEntry(key) >= 0;

        public bool ContainsValue(TValue value)
        {
            var comparer = EqualityComparer<TValue>.Default;
            for (int i = 0; i < _count; i++)
                if (_entries[i].Next >= -1 && comparer.Equals(_entries[i].Value, value))
                    return true;
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int i = FindEntry(key);
            if (i >= 0)
            {
                value = _entries[i].Value;
                return true;
            }
            value = default;
            return false;
        }

        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
            => TryGetValue(key, out TValue value) ? value : defaultValue;

        public bool Remove(TKey key)
        {
            if (key is null)
                ThrowHelpers.ThrowArgumentNullException();
            if (_buckets == null)
                return false;
            uint hash = Hash(key);
            ref int bucket = ref _buckets[hash & (uint)(_buckets.Length - 1)];
            int last = -1;
            for (int i = bucket - 1; i >= 0; last = i, i = _entries[i].Next)
            {
                ref Entry e = ref _entries[i];
                if (e.HashCode == hash && KeyEquals(e.Key, key))
                {
                    if (last < 0)
                        bucket = e.Next + 1;
                    else
                        _entries[last].Next = e.Next;
                    // Free entries are marked with Next < -1; the free list is encoded as -3 - nextFree.
                    e.Next = -3 - _freeList;
                    e.Key = default;
                    e.Value = default;
                    _freeList = i;
                    _freeCount++;
                    _version++;
                    return true;
                }
            }
            return false;
        }

        public bool Remove(TKey key, out TValue value)
        {
            if (TryGetValue(key, out value))
                return Remove(key);
            return false;
        }

        public void Clear()
        {
            if (_count > 0)
            {
                Array.Clear(_buckets);
                Array.Clear(_entries, 0, _count);
                _count = 0;
                _freeList = -1;
                _freeCount = 0;
                _version++;
            }
        }

        public KeyCollection Keys => new KeyCollection(this);
        public ValueCollection Values => new ValueCollection(this);

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly Dictionary<TKey, TValue> _dictionary;
            private readonly int _version;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;

            internal Enumerator(Dictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = 0;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                    ThrowHelpers.ThrowInvalidOperationException("Collection was modified; enumeration operation may not execute.");
                while ((uint)_index < (uint)_dictionary._count)
                {
                    ref Entry entry = ref _dictionary._entries[_index++];
                    if (entry.Next >= -1)
                    {
                        _current = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
                        return true;
                    }
                }
                _index = _dictionary._count + 1;
                _current = default;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current => _current;
            object IEnumerator.Current => _current;
            public void Reset() { _index = 0; _current = default; }
            public void Dispose() { }
        }

        public readonly struct KeyCollection
        {
            private readonly Dictionary<TKey, TValue> _dictionary;
            internal KeyCollection(Dictionary<TKey, TValue> dictionary) { _dictionary = dictionary; }
            public int Count => _dictionary.Count;
            public KeyEnumerator GetEnumerator() => new KeyEnumerator(_dictionary);
        }

        public struct KeyEnumerator
        {
            private Enumerator _inner;
            internal KeyEnumerator(Dictionary<TKey, TValue> dictionary) { _inner = new Enumerator(dictionary); }
            public bool MoveNext() => _inner.MoveNext();
            public TKey Current => _inner.Current.Key;
        }

        public readonly struct ValueCollection
        {
            private readonly Dictionary<TKey, TValue> _dictionary;
            internal ValueCollection(Dictionary<TKey, TValue> dictionary) { _dictionary = dictionary; }
            public int Count => _dictionary.Count;
            public ValueEnumerator GetEnumerator() => new ValueEnumerator(_dictionary);
        }

        public struct ValueEnumerator
        {
            private Enumerator _inner;
            internal ValueEnumerator(Dictionary<TKey, TValue> dictionary) { _inner = new Enumerator(dictionary); }
            public bool MoveNext() => _inner.MoveNext();
            public TValue Current => _inner.Current.Value;
        }
    }

    public class HashSet<T> : IReadOnlyCollection<T>
    {
        private readonly Dictionary<T, bool> _map;

        public HashSet() { _map = new Dictionary<T, bool>(); }
        public HashSet(IEqualityComparer<T> comparer) { _map = new Dictionary<T, bool>(comparer); }

        public int Count => _map.Count;
        public bool Add(T item) => _map.TryAdd(item, true);
        public bool Remove(T item) => _map.Remove(item);
        public bool Contains(T item) => _map.ContainsKey(item);
        public void Clear() => _map.Clear();

        public Enumerator GetEnumerator() => new Enumerator(_map);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        public struct Enumerator : IEnumerator<T>
        {
            private Dictionary<T, bool>.Enumerator _inner;
            internal Enumerator(Dictionary<T, bool> map) { _inner = map.GetEnumerator(); }
            public bool MoveNext() => _inner.MoveNext();
            public T Current => _inner.Current.Key;
            object IEnumerator.Current => Current;
            public void Reset() => _inner.Reset();
            public void Dispose() { }
        }
    }

    public class Queue<T> : IReadOnlyCollection<T>
    {
        private T[] _array = Array.Empty<T>();
        private int _head;
        private int _size;

        public Queue() { }
        public Queue(int capacity) { _array = new T[capacity]; }

        public int Count => _size;

        public void Enqueue(T item)
        {
            if (_size == _array.Length)
                Grow();
            _array[(_head + _size) % _array.Length] = item;
            _size++;
        }

        public T Dequeue()
        {
            if (_size == 0)
                ThrowHelpers.ThrowInvalidOperationException("Queue empty.");
            T item = _array[_head];
            _array[_head] = default;
            _head = (_head + 1) % _array.Length;
            _size--;
            return item;
        }

        public bool TryDequeue(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }
            result = Dequeue();
            return true;
        }

        public T Peek()
        {
            if (_size == 0)
                ThrowHelpers.ThrowInvalidOperationException("Queue empty.");
            return _array[_head];
        }

        public void Clear()
        {
            Array.Clear(_array);
            _head = 0;
            _size = 0;
        }

        private void Grow()
        {
            int capacity = _array.Length == 0 ? 4 : _array.Length * 2;
            T[] newArray = new T[capacity];
            for (int i = 0; i < _size; i++)
                newArray[i] = _array[(_head + i) % _array.Length];
            _array = newArray;
            _head = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _size; i++)
                yield return _array[(_head + i) % _array.Length];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class Stack<T> : IReadOnlyCollection<T>
    {
        private T[] _array = Array.Empty<T>();
        private int _size;

        public Stack() { }
        public Stack(int capacity) { _array = new T[capacity]; }

        public int Count => _size;

        public void Push(T item)
        {
            if (_size == _array.Length)
                Array.Resize(ref _array, _array.Length == 0 ? 4 : _array.Length * 2);
            _array[_size++] = item;
        }

        public T Pop()
        {
            if (_size == 0)
                ThrowHelpers.ThrowInvalidOperationException("Stack empty.");
            T item = _array[--_size];
            _array[_size] = default;
            return item;
        }

        public bool TryPop(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }
            result = Pop();
            return true;
        }

        public T Peek()
        {
            if (_size == 0)
                ThrowHelpers.ThrowInvalidOperationException("Stack empty.");
            return _array[_size - 1];
        }

        public void Clear()
        {
            Array.Clear(_array);
            _size = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = _size - 1; i >= 0; i--)
                yield return _array[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
