using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaskbarFetch.GeometryTests;

[TestClass]
public sealed class PendingClickStoreTests
{
    [TestMethod]
    public void TryTake_DoesNotRemoveNewerClick_WhenOlderEvaluationFinishes()
    {
        var store = new PendingClickStore<ClickState>(state => state.Clone());
        store.Replace(1, new ClickState { Value = 1 });
        Assert.IsTrue(store.TryGetSnapshot(out var olderId, out _));

        store.Replace(2, new ClickState { Value = 2 });

        Assert.IsFalse(store.TryTake(olderId, out _));
        Assert.IsTrue(store.TryGetSnapshot(out var currentId, out var current));
        Assert.AreEqual(2, currentId);
        Assert.AreEqual(2, current.Value);
    }

    [TestMethod]
    public void TryUpdate_RejectsAnOlderClickId()
    {
        var store = new PendingClickStore<ClickState>(state => state.Clone());
        store.Replace(10, new ClickState { Value = 10 });
        Assert.IsTrue(store.TryGetSnapshot(out var olderId, out _));
        store.Replace(11, new ClickState { Value = 11 });

        Assert.IsFalse(store.TryUpdate(olderId, state => state.Value = 99));
        Assert.IsTrue(store.TryGetSnapshot(out var currentId, out var current));
        Assert.AreEqual(11, currentId);
        Assert.AreEqual(11, current.Value);
    }

    [TestMethod]
    public void TryGetSnapshot_ReturnsAnIndependentCopy()
    {
        var store = new PendingClickStore<ClickState>(state => state.Clone());
        store.Replace(20, new ClickState { Value = 20 });
        Assert.IsTrue(store.TryGetSnapshot(out _, out var snapshot));

        snapshot.Value = 99;

        Assert.IsTrue(store.TryGetSnapshot(out _, out var current));
        Assert.AreEqual(20, current.Value);
    }

    private sealed class ClickState
    {
        public int Value;

        public ClickState Clone()
        {
            return new ClickState { Value = Value };
        }
    }
}
