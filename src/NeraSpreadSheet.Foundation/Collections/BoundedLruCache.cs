namespace NeraSpreadSheet.Foundation.Collections;

/// <summary>
/// A small non-thread-safe least-recently-used cache with a fixed capacity.
/// </summary>
public sealed class BoundedLruCache<TKey, TValue> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _recency = [];
    private readonly Action<TValue>? _releaseValue;
    private bool _disposed;

    public BoundedLruCache(int capacity, Action<TValue>? releaseValue = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        _releaseValue = releaseValue;
    }

    public int Capacity { get; }

    public int Count => _entries.Count;

    public long HitCount { get; private set; }

    public long MissCount { get; private set; }

    public long EvictionCount { get; private set; }

    public bool TryGetValue(TKey key, out TValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_entries.TryGetValue(key, out var node))
        {
            Promote(node);
            HitCount++;
            value = node.Value.Value;
            return true;
        }

        MissCount++;
        value = default!;
        return false;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(valueFactory);

        if (_entries.TryGetValue(key, out var existing))
        {
            Promote(existing);
            HitCount++;
            return existing.Value.Value;
        }

        MissCount++;
        var value = valueFactory(key);
        var node = _recency.AddFirst(new Entry(key, value));
        _entries.Add(key, node);
        TrimToCapacity();
        return value;
    }

    public bool Remove(TKey key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_entries.Remove(key, out var node))
        {
            return false;
        }

        _recency.Remove(node);
        Release(node.Value.Value);
        return true;
    }

    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ReleaseAll();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseAll();
        _disposed = true;
    }

    private void Promote(LinkedListNode<Entry> node)
    {
        if (ReferenceEquals(_recency.First, node))
        {
            return;
        }

        _recency.Remove(node);
        _recency.AddFirst(node);
    }

    private void TrimToCapacity()
    {
        while (_entries.Count > Capacity)
        {
            var node = _recency.Last
                ?? throw new InvalidOperationException("LRU cache recency list is unexpectedly empty.");
            _recency.RemoveLast();
            _entries.Remove(node.Value.Key);
            EvictionCount++;
            Release(node.Value.Value);
        }
    }

    private void ReleaseAll()
    {
        foreach (var entry in _recency)
        {
            Release(entry.Value);
        }
        _recency.Clear();
        _entries.Clear();
    }

    private void Release(TValue value) => _releaseValue?.Invoke(value);

    private sealed record Entry(TKey Key, TValue Value);
}
