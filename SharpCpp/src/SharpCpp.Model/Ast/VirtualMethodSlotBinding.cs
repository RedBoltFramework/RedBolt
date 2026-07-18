namespace SharpCpp.Model.Ast;

public sealed class VirtualMethodSlotBinding
{
    public VirtualMethodSlotBinding(
        FunctionBinding method,
        RecordBinding nativeImplementationRecord,
        RecordBinding managedCallbackOwnerRecord,
        FunctionBinding managedCallbackMethod)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(nativeImplementationRecord);
        ArgumentNullException.ThrowIfNull(managedCallbackOwnerRecord);
        ArgumentNullException.ThrowIfNull(managedCallbackMethod);

        Method = method;
        NativeImplementationRecord = nativeImplementationRecord;
        ManagedCallbackOwnerRecord = managedCallbackOwnerRecord;
        ManagedCallbackMethod = managedCallbackMethod;
    }

    public FunctionBinding Method { get; }

    public RecordBinding NativeImplementationRecord { get; }

    public RecordBinding ManagedCallbackOwnerRecord { get; }

    public FunctionBinding ManagedCallbackMethod { get; }
}
