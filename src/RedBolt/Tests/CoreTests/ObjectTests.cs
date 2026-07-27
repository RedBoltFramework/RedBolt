namespace RedBolt.Tests;

public sealed class ObjectTests
{
    [Fact]
    public void Object_forwards_dynamic_subsystem_access_to_context()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var subsystem = new TestSubsystem();
            var type = new StringHash("TestSubsystem");
            context.RegisterSubsystem(subsystem, type);
            var receiver = new TestObject();

            Assert.Same(subsystem, receiver.GetSubsystem(type));
            Assert.Same(subsystem, receiver.GetSubsystems().Get(type));
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }

    [Fact]
    public void Object_default_archive_serialization_is_not_supported()
    {
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            var archive = new TestArchive(context);
            var exception = Assert.Throws<InvalidOperationException>(() => new TestObject().SerializeInBlock(archive));

            Assert.Contains("TestObject", exception.Message);
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }

    [Fact]
    public void Object_dispose_removes_receiver_and_sender_event_links()
    {
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            var sender = new TestObject();
            var receiver = new TestObject();
            var calls = 0;

            receiver.SubscribeToEvent<DisposeEvent>(sender, (ref DisposeEvent _) => ++calls);

            var args = new DisposeEvent();
            sender.SendEvent(ref args);
            Assert.Equal(1, calls);

            sender.Dispose();
            sender.SendEvent(ref args);
            Assert.Equal(1, calls);

            receiver.Dispose();
            Assert.False(context.HasSubscribedToEvent(receiver));
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }

    private sealed class TestObject : RedBolt.Object
    {
    }

    private sealed class TestSubsystem
    {
    }

    private struct DisposeEvent
    {
    }

    private sealed class TestArchive : Archive
    {
        public TestArchive(Context context) => Context = context;

        public override Context Context { get; }
        public override string Name => string.Empty;
        public override uint Checksum => 0;
        public override bool IsInput => false;
        public override bool IsHumanReadable => false;
        public override bool IsUnorderedAccessSupportedInCurrentBlock => false;
        public override bool IsEof => false;
        public override string CurrentBlockPath => string.Empty;
        public override bool HasElementOrBlock(string name) => false;
        public override void BeginBlock(string name, ref int sizeHint, bool safe, ArchiveBlockType type) { }
        public override void EndBlock() { }
        public override void Flush() { }
        public override void Serialize(string name, ref bool value) { }
        public override void Serialize(string name, ref sbyte value) { }
        public override void Serialize(string name, ref byte value) { }
        public override void Serialize(string name, ref short value) { }
        public override void Serialize(string name, ref ushort value) { }
        public override void Serialize(string name, ref int value) { }
        public override void Serialize(string name, ref uint value) { }
        public override void Serialize(string name, ref long value) { }
        public override void Serialize(string name, ref ulong value) { }
        public override void Serialize(string name, ref float value) { }
        public override void Serialize(string name, ref double value) { }
        public override void Serialize(string name, ref string value) { }
        public override void SerializeBytes(string name, Span<byte> bytes) { }
        public override void SerializeVle(string name, ref uint value) { }
        public override uint SerializeVersion(uint version) => version;
    }
}
