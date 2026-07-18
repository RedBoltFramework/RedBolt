using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SharpCpp.Support;

namespace Urho3D.Core;

public partial class WorkQueue
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ManagedTaskCallback(nint callbackHandle, uint threadIndex, nint workQueue);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ManagedTaskRelease(nint callbackHandle);

    private static readonly ManagedTaskCallback s_managedTaskCallback = DispatchManagedTask;
    private static readonly nint s_managedTaskCallbackPointer = Marshal.GetFunctionPointerForDelegate(s_managedTaskCallback);
    private static readonly ManagedTaskRelease s_managedTaskRelease = ReleaseManagedTask;
    private static readonly nint s_managedTaskReleasePointer = Marshal.GetFunctionPointerForDelegate(s_managedTaskRelease);

    public void PostTaskForMainThread(Action<uint, WorkQueue> taskFunction, TaskPriority priority = TaskPriority.Medium)
    {
        ArgumentNullException.ThrowIfNull(taskFunction);

        var callbackHandle = GCHandle.ToIntPtr(GCHandle.Alloc(taskFunction));
        __WorkQueuePInvoke.PostTaskForMainThread(
            NativeObject.GetHandle(this),
            s_managedTaskCallbackPointer,
            s_managedTaskReleasePointer,
            callbackHandle,
            priority);
    }

    public void PostDelayedTaskForMainThread(Action<uint, WorkQueue> taskFunction)
    {
        ArgumentNullException.ThrowIfNull(taskFunction);

        var callbackHandle = GCHandle.ToIntPtr(GCHandle.Alloc(taskFunction));
        __WorkQueuePInvoke.PostDelayedTaskForMainThread(
            NativeObject.GetHandle(this),
            s_managedTaskCallbackPointer,
            s_managedTaskReleasePointer,
            callbackHandle);
    }

    private static void DispatchManagedTask(nint callbackHandle, uint threadIndex, nint workQueue)
    {
        var taskFunction = (Action<uint, WorkQueue>)GCHandle.FromIntPtr(callbackHandle).Target!;
        taskFunction(threadIndex, CreateManagedWrapper(workQueue, ownsHandle: false));
    }

    private static void ReleaseManagedTask(nint callbackHandle)
    {
        if (callbackHandle == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(callbackHandle);
        if (handle.IsAllocated)
        {
            handle.Free();
        }
    }
}

internal static partial class __WorkQueuePInvoke
{
    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "SharpCpp_Rbfx_WorkQueue_PostTaskForMainThread")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void PostTaskForMainThread(nint receiver, nint callback, nint release, nint callbackHandle, TaskPriority priority);

    [LibraryImport("SharpCpp.Rbfx.Native", EntryPoint = "SharpCpp_Rbfx_WorkQueue_PostDelayedTaskForMainThread")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static partial void PostDelayedTaskForMainThread(nint receiver, nint callback, nint release, nint callbackHandle);
}
