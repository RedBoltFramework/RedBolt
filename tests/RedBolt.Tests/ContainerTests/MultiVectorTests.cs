namespace RedBolt.Tests.ContainerTests;

public class MultiVectorTests
{
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

        values.Clear(2);

        Assert.Equal(2, values.GetUnderlyingCollection().Count);
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
}
