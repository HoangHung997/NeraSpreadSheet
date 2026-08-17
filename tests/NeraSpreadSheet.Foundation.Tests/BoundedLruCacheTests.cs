using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation.Collections;

namespace NeraSpreadSheet.Foundation.Tests;

[TestClass]
public sealed class BoundedLruCacheTests
{
    private static readonly string[] ExpectedSingleEviction = ["two"];
    private static readonly int[] ExpectedReleasedValues = [1, 2, 3];

    [TestMethod]
    public void GetOrAddEvictsLeastRecentlyUsedEntryAndReleasesIt()
    {
        var released = new List<string>();
        using var cache = new BoundedLruCache<int, string>(2, released.Add);

        cache.GetOrAdd(1, _ => "one");
        cache.GetOrAdd(2, _ => "two");
        Assert.IsTrue(cache.TryGetValue(1, out _));
        cache.GetOrAdd(3, _ => "three");

        Assert.IsTrue(cache.TryGetValue(1, out var one));
        Assert.AreEqual("one", one);
        Assert.IsFalse(cache.TryGetValue(2, out _));
        Assert.IsTrue(cache.TryGetValue(3, out var three));
        Assert.AreEqual("three", three);
        CollectionAssert.AreEqual(ExpectedSingleEviction, released);
        Assert.AreEqual(1L, cache.EvictionCount);
    }

    [TestMethod]
    public void ExistingEntryIsReturnedWithoutCallingFactoryAgain()
    {
        using var cache = new BoundedLruCache<string, int>(4);
        var calls = 0;

        var first = cache.GetOrAdd("A", _ => { calls++; return 42; });
        var second = cache.GetOrAdd("A", _ => { calls++; return 99; });

        Assert.AreEqual(42, first);
        Assert.AreEqual(42, second);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(1L, cache.HitCount);
        Assert.AreEqual(1L, cache.MissCount);
    }

    [TestMethod]
    public void ClearAndDisposeReleaseResidentValuesExactlyOnce()
    {
        var released = new List<int>();
        var cache = new BoundedLruCache<int, int>(4, released.Add);
        cache.GetOrAdd(1, key => key);
        cache.GetOrAdd(2, key => key);

        cache.Clear();
        cache.GetOrAdd(3, key => key);
        cache.Dispose();
        cache.Dispose();

        CollectionAssert.AreEquivalent(ExpectedReleasedValues, released);
        Assert.AreEqual(3, released.Count);
    }
}
