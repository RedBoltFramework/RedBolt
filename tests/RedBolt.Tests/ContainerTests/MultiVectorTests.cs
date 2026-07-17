namespace RedBolt.Tests.ContainerTests;

public class MultiVectorTests
{
    private sealed class CloneableValue(int value) : ICloneable
    {
        public int Value = value;

        public object Clone() => new CloneableValue(Value);
    }

    private sealed class DisposableValue : ICloneable, IDisposable
    {
        public bool IsDisposed;

        public object Clone() => new DisposableValue();

        public void Dispose() => IsDisposed = true;
    }

    [Fact]
    public void OperationsMatchRbfxContract()
    {
        var values = new MultiVector<int>();
        values.Clear(3);

        var first = values.PushBack(0, 1);
        values.PushBack(2, 3);
        values.EmplaceBack(2, () => 4);

        Assert.Equal(new MultiVector<int>.Index(0, 0), first);
        Assert.Equal(3u, values.Size());
        Assert.Equal(1, values[first]);
        Assert.Equal([1, 3, 4], values.ToArray());

        values.PopBack(2);
        Assert.Equal([1, 3], values.ToArray());
    }

    [Fact]
    public void ClearPreservesOuterSizeAndClearsEveryInnerVector()
    {
        var values = new MultiVector<int>();
        values.Resize(2);
        values.PushBack(0, 1);
        values.PushBack(1, 2);

        var first = values.GetUnderlyingCollection()[0];
        var second = values.GetUnderlyingCollection()[1];

        values.Clear(2);

        Assert.Equal(2, values.GetUnderlyingCollection().Count);
        Assert.Same(first, values.GetUnderlyingCollection()[0]);
        Assert.Same(second, values.GetUnderlyingCollection()[1]);
        Assert.All(values.GetUnderlyingCollection(), Assert.Empty);
    }

    [Fact]
    public void CopyToReplacesDestinationLikeRbfx()
    {
        var values = new MultiVector<int>();
        values.Resize(2);
        values.PushBack(0, 1);
        values.PushBack(1, 2);
        var destination = new List<int> { 99 };

        values.CopyTo(destination);

        Assert.Equal([1, 2], destination);
    }

    [Fact]
    public void UnderlyingCollectionIsMutableLikeRbfx()
    {
        var values = new MultiVector<int>();
        values.Resize(1);

        values.GetUnderlyingCollection()[0].Add(42);

        Assert.Equal([42], values.ToArray());
    }

    [Fact]
    public void EmplaceAndIndexerReturnMutableReferences()
    {
        var values = new MultiVector<int>();
        values.Resize(1);

        ref var emplaced = ref values.EmplaceBack(0, () => 1);
        emplaced = 2;
        var index = new MultiVector<int>.Index(0, 0);
        ref var indexed = ref values[index];
        indexed = 3;

        Assert.Equal([3], values.ToArray());
    }

    [Fact]
    public void CopiesCloneValueObjects()
    {
        var values = new MultiVector<CloneableValue>();
        values.Resize(1);
        var source = new CloneableValue(1);

        var index = values.PushBack(0, source);
        var destination = new List<CloneableValue>();
        values.CopyTo(destination);
        var clone = (MultiVector<CloneableValue>)values.Clone();

        source.Value = 2;
        Assert.Equal(1, values[index].Value);
        Assert.NotSame(source, values[index]);
        Assert.NotSame(values[index], destination[0]);
        Assert.NotSame(values[index], clone[index]);
    }

    [Fact]
    public void DestructionOperationsDisposeOwnedValueObjects()
    {
        var values = new MultiVector<DisposableValue>();
        values.Resize(2);
        var popped = values.EmplaceBack(0, () => new DisposableValue());
        var resized = values.EmplaceBack(1, () => new DisposableValue());

        values.PopBack(0);
        values.Resize(1);

        Assert.True(popped.IsDisposed);
        Assert.True(resized.IsDisposed);

        var cleared = values.EmplaceBack(0, () => new DisposableValue());
        values.Clear(1);
        Assert.True(cleared.IsDisposed);

        var disposed = values.EmplaceBack(0, () => new DisposableValue());
        values.Dispose();
        Assert.True(disposed.IsDisposed);
    }
}
