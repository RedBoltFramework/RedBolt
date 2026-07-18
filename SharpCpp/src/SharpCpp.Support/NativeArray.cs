using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;

namespace SharpCpp;

public partial class NativeArray<T> : global::SharpCpp.Support.ArrayBase<T>
{
    public delegate global::SharpCpp.Support.NativeArrayAbi __CreateDelegate();
    public delegate global::SharpCpp.Support.NativeArrayAbi __DynamicCreateDelegate(nuint length);
    public delegate void __DeleteDelegate(global::SharpCpp.Support.NativeArrayAbi self);
    public delegate T __GetDelegate(global::SharpCpp.Support.NativeArrayAbi self, nuint index);
    public delegate void __SetDelegate(global::SharpCpp.Support.NativeArrayAbi self, nuint index, T value);

    private sealed record Operations(__DynamicCreateDelegate Create, __DeleteDelegate Delete, __GetDelegate Get, __SetDelegate Set);
    private readonly Operations? _operations;

    private static readonly global::SharpCpp.Support.DefaultNativeInstanceCache s_instanceCache = new();
    private static readonly ConcurrentDictionary<int, Operations> s_operations = new();
    private static Operations? s_dynamicOperations;

    internal NativeArray(nint handle, int length, bool ownsHandle = false)
        : this(handle, length, ownsHandle, TryGetOperations(length, out var operations) ? operations : null)
    {
    }

    private NativeArray(nint handle, int length, bool ownsHandle, Operations? operations)
        : base(handle, length, ownsHandle)
    {
        _operations = operations;
        if (!IsInvalid)
        {
            s_instanceCache.Register(this);
        }
    }

    protected override global::SharpCpp.Support.INativeInstanceCache? InstanceCache => s_instanceCache;

    public nint Handle => DangerousGetHandle();
    public bool OwnsHandle => OwnsNativeHandle;
    public bool IsNull => IsClosed || IsInvalid;

    public NativeArray(int length)
        : this(CreateNative(length), ownsHandle: true)
    {
    }

    public NativeArray(ReadOnlySpan<T> values)
        : this(values.Length)
    {
        Assign(values);
    }

    public NativeArray(IEnumerable<T> values)
        : this(Materialize(values))
    {
    }

    private NativeArray(global::SharpCpp.Support.NativeArrayAbi abi, bool ownsHandle)
        : this(abi.Handle, checked((int)abi.Length), ownsHandle, TryGetOperations(checked((int)abi.Length), out var operations) ? operations : null)
    {
    }

    private NativeArray(T[] values)
        : this(values.AsSpan())
    {
    }

    private static T[] Materialize(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ToArray();
    }

    public static void __Register(
        int length,
        __CreateDelegate create,
        __DeleteDelegate delete,
        __GetDelegate get,
        __SetDelegate set)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);

        s_operations.TryAdd(length, new Operations(_ => create(), delete, get, set));
    }

    public static void __RegisterDynamic(
        __DynamicCreateDelegate create,
        __DeleteDelegate delete,
        __GetDelegate get,
        __SetDelegate set)
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(delete);
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        Interlocked.CompareExchange(ref s_dynamicOperations, new Operations(create, delete, get, set), null);
    }

    public static NativeArray<T> CreateManagedWrapper(global::SharpCpp.Support.NativeArrayAbi abi, bool ownsHandle)
    {
        return __CreateManagedWrapper(abi, ownsHandle);
    }

    public static NativeArray<T> __CreateManagedWrapper(global::SharpCpp.Support.NativeArrayAbi abi, bool ownsHandle)
    {
        if (abi.Handle == 0)
        {
            return new NativeArray<T>(abi, ownsHandle);
        }

        if (s_instanceCache.TryGetExisting(abi.Handle, ownsHandle, out var existing) && existing is NativeArray<T> cached)
        {
            return cached;
        }

        return new NativeArray<T>(abi, ownsHandle);
    }

    public static NativeArray<T> CreateManagedWrapper(nint handle, bool ownsHandle)
    {
        return __CreateManagedWrapper(handle, ownsHandle);
    }

    public static NativeArray<T> __CreateManagedWrapper(nint handle, bool ownsHandle)
    {
        return __CreateManagedWrapper(new global::SharpCpp.Support.NativeArrayAbi(handle, 0), ownsHandle);
    }

    protected override T GetAtCore(nuint index)
    {
        return (_operations ?? GetOperations(Length)).Get(ToNativeArrayAbi(), index);
    }

    protected override void SetAtCore(nuint index, T value)
    {
        (_operations ?? GetOperations(Length)).Set(ToNativeArrayAbi(), index, value);
    }

    protected override bool ReleaseNativeHandle(nint handle)
    {
        if (!OwnsHandle)
        {
            return true;
        }

        if ((_operations ?? (TryGetOperations(Length, out var operations) ? operations : null)) is { } resolvedOperations)
        {
            resolvedOperations.Delete(new global::SharpCpp.Support.NativeArrayAbi(handle, checked((nuint)Length)));
        }

        return true;
    }

    private static global::SharpCpp.Support.NativeArrayAbi CreateNative(int length)
    {
        return GetOperations(length).Create(checked((nuint)length));
    }

    private static bool TryGetOperations(int length, out Operations? operations)
    {
        if (s_operations.TryGetValue(length, out operations))
            return true;
        operations = s_dynamicOperations;
        return operations is not null;
    }

    private static Operations GetOperations(int length)
    {
        if (TryGetOperations(length, out var operations) && operations is not null)
        {
            return operations;
        }

        throw new NotSupportedException($"Unsupported NativeArray instantiation: {typeof(T).FullName}[{length}].");
    }
}

[CustomMarshaller(typeof(NativeArray<>), MarshalMode.ManagedToUnmanagedIn, typeof(__NativeArrayInMarshaller<>))]
public static class __NativeArrayInMarshaller<T>
{
    public static global::SharpCpp.Support.NativeArrayAbi ConvertToUnmanaged(NativeArray<T> managed)
    {
        ArgumentNullException.ThrowIfNull(managed);
        return managed.ToNativeArrayAbi();
    }
}

[CustomMarshaller(typeof(NativeArray<>), MarshalMode.ManagedToUnmanagedOut, typeof(__NativeArrayReturnMarshaller<>))]
public static class __NativeArrayReturnMarshaller<T>
{
    public static NativeArray<T> ConvertToManaged(global::SharpCpp.Support.NativeArrayAbi unmanaged)
    {
        return NativeArray<T>.CreateManagedWrapper(unmanaged, ownsHandle: false);
    }
}

[CustomMarshaller(typeof(NativeArray<>), MarshalMode.ManagedToUnmanagedOut, typeof(__NativeArrayOwnedReturnMarshaller<>))]
public static class __NativeArrayOwnedReturnMarshaller<T>
{
    public static NativeArray<T> ConvertToManaged(global::SharpCpp.Support.NativeArrayAbi unmanaged)
    {
        return NativeArray<T>.CreateManagedWrapper(unmanaged, ownsHandle: true);
    }
}
