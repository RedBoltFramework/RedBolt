using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum TaskPriority
{
    Immediate,
    Highest,
    High,
    Medium,
    Low,
}

public delegate void TaskFunctionDelegate(int threadIndex, WorkQueue queue);

[Obsolete("Use TaskFunctionDelegate instead.")]
public delegate void WorkFunctionDelegate(int threadIndex);

[Obsolete("WorkItem is deprecated. Use TaskFunctionDelegate instead.")]
public sealed class WorkItem
{
    public Action<WorkItem, int>? WorkFunction;
    public IntPtr Start;
    public IntPtr End;
    public IntPtr Aux;
    public uint Priority;
    internal Action<uint>? WorkLambda;
}

public sealed class WorkQueue : Object
{
    private static uint _threadIndexCount = 1;
    private static WorkQueue? _workQueue;

    private uint _numProcessingThreads;

    private readonly List<(TaskPriority Priority, TaskFunctionDelegate? Task)> _fallbackTaskQueue = [];

    private List<TaskFunctionDelegate> _mainThreadTasks = [];
    private List<TaskFunctionDelegate> _mainThreadTasksSwap = [];
    private readonly object _mainThreadTasksLock = new();

    private int _maxNonThreadedWorkMs = 5;

    private int _pendingTaskCount;

    private List<List<TaskFunctionDelegate>> _pendingImmediateTasks = [];

    public WorkQueue()
    {
        if (_workQueue == null)
        {
            _threadIndexCount = 1;
            _workQueue = this;
        }

        SubscribeToEvent<CoreEvents.BeginFrameEvent>((ref CoreEvents.BeginFrameEvent _) => Update());
    }

    public void Initialize(uint numThreads)
    {
        if (_numProcessingThreads > 0)
            return;

        _numProcessingThreads = numThreads + 1;
        _threadIndexCount = 1;

        if (numThreads > 0)
        {
            ThreadPool.SetMinThreads((int)_numProcessingThreads, (int)_numProcessingThreads);

            for (var i = 1u; i < _numProcessingThreads; ++i)
            {
                PostTaskForThread(_ => { }, TaskPriority.Immediate, i);
            }

            CompleteAll();

            _threadIndexCount = _numProcessingThreads;
        }
    }

    public void Update()
    {
        ProcessPostedTasks();
        ProcessMainThreadTasks();
    }

    public void PostTask(Action<int> task, TaskPriority priority = TaskPriority.Medium)
    {
        PostTask((threadIndex, _) => task(threadIndex), priority);
    }

    public void PostTask(Action task, TaskPriority priority = TaskPriority.Medium)
    {
        PostTask((_, _) => task(), priority);
    }

    public void PostTask(TaskFunctionDelegate task, TaskPriority priority = TaskPriority.Medium)
    {
        if (!IsProcessingThread())
        {
            var capturedTask = task;
            var capturedPriority = priority;
            TaskFunctionDelegate wrapped = (_, queue) => queue.PostTask(capturedTask, capturedPriority);
            PostTaskForMainThread(wrapped, priority);
            return;
        }

        if (_numProcessingThreads > 1)
        {
            if (priority == TaskPriority.Immediate)
            {
                var threadIndex = (int)GetThreadIndex();
                EnsurePendingImmediateSize(threadIndex + 1);
                _pendingImmediateTasks[threadIndex].Add(task);
            }
            else
            {
                Interlocked.Increment(ref _pendingTaskCount);
                ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    try
                    {
                        task((int)GetThreadIndex(), this);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingTaskCount);
                    }
                }, null);
            }
            return;
        }

        if (priority == TaskPriority.Immediate)
            task(0, this);
        else
            _fallbackTaskQueue.Add((priority, task));
    }

    public void PostTaskForThread(Action<int> task, TaskPriority priority, uint threadIndex)
    {
        PostTaskForThread((threadIdx, _) => task(threadIdx), priority, threadIndex);
    }

    public void PostTaskForThread(Action task, TaskPriority priority, uint threadIndex)
    {
        PostTaskForThread((_, _) => task(), priority, threadIndex);
    }

    public void PostTaskForThread(TaskFunctionDelegate task, TaskPriority priority, uint threadIndex)
    {
        if (!IsProcessingThread())
        {
            var capturedTask = task;
            var capturedPriority = priority;
            var capturedThreadIndex = threadIndex;
            TaskFunctionDelegate wrapped =
                (_, queue) => queue.PostTaskForThread(capturedTask, capturedPriority, capturedThreadIndex);
            PostTaskForMainThread(wrapped, priority);
            return;
        }

        if (_numProcessingThreads > 1)
        {
            Interlocked.Increment(ref _pendingTaskCount);
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    task((int)GetThreadIndex(), this);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingTaskCount);
                }
            }, null);
            return;
        }

        if (priority == TaskPriority.Immediate)
            task(0, this);
        else
            _fallbackTaskQueue.Add((priority, task));
    }

    public void PostTaskForMainThread(Action<int> task, TaskPriority priority = TaskPriority.Medium)
    {
        PostTaskForMainThread((threadIdx, _) => task(threadIdx), priority);
    }

    public void PostTaskForMainThread(Action task, TaskPriority priority = TaskPriority.Medium)
    {
        PostTaskForMainThread((_, _) => task(), priority);
    }

    public void PostTaskForMainThread(TaskFunctionDelegate task, TaskPriority priority = TaskPriority.Medium)
    {
        if (_numProcessingThreads > 1 && IsProcessingThread())
        {
            PostTaskForThread(task, priority, 0);
            return;
        }

        if (Thread.CurrentThread.ManagedThreadId == GetMainThreadId())
            task(0, this);
        else
            PostDelayedTaskForMainThread(task);
    }

    public void PostDelayedTaskForMainThread(Action<int> task)
    {
        PostDelayedTaskForMainThread((threadIdx, _) => task(threadIdx));
    }

    public void PostDelayedTaskForMainThread(Action task)
    {
        PostDelayedTaskForMainThread((_, _) => task());
    }

    public void PostDelayedTaskForMainThread(TaskFunctionDelegate task)
    {
        lock (_mainThreadTasksLock)
        {
            _mainThreadTasks.Add(task);
        }
    }

    public void CompleteImmediateForThisThread()
    {
        if (_numProcessingThreads > 1)
        {
            var threadIndex = (int)GetThreadIndex();
            EnsurePendingImmediateSize(threadIndex + 1);
            var pending = _pendingImmediateTasks[threadIndex];
            if (pending.Count > 0)
            {
                var tasksToRun = pending.ToArray();
                pending.Clear();
                foreach (var task in tasksToRun)
                    task(threadIndex, this);
            }
        }
    }

    public void CompleteAll()
    {
        CompleteImmediateForThisThread();

        while (Interlocked.CompareExchange(ref _pendingTaskCount, 0, 0) > 0)
        {
            Thread.Sleep(0);
        }

        if (_fallbackTaskQueue.Count > 0)
        {
            foreach (var (_, task) in _fallbackTaskQueue)
                task?.Invoke(0, this);
            _fallbackTaskQueue.Clear();
        }

        while (ProcessMainThreadTasks())
        {
        }
    }

    public uint GetNumIncomplete()
    {
        var result = (uint)_fallbackTaskQueue.Count;
        result += (uint)Volatile.Read(ref _pendingTaskCount);
        foreach (var pending in _pendingImmediateTasks)
            result += (uint)pending.Count;
        return result;
    }

    public bool IsCompleted()
    {
        return GetNumIncomplete() == 0;
    }

    public void SetNonThreadedWorkMs(int ms)
    {
        _maxNonThreadedWorkMs = Math.Max(ms, 1);
    }

    public int GetNonThreadedWorkMs()
    {
        return _maxNonThreadedWorkMs;
    }

    public uint GetNumProcessingThreads()
    {
        return _numProcessingThreads;
    }

    public bool IsMultithreaded()
    {
        return _numProcessingThreads > 1;
    }

    public static uint GetThreadIndex()
    {
        return _workQueue?.GetThreadIndexImpl() ?? 0;
    }

    public static uint GetThreadIndexCount()
    {
        return _threadIndexCount;
    }

    public static bool IsProcessingThread()
    {
        return GetThreadIndex() < _threadIndexCount;
    }

    public static void ForEachParallel(WorkQueue workQueue, uint bucket, uint size, Action<uint, uint> callback)
    {
        ArgumentNullException.ThrowIfNull(workQueue);
        ArgumentNullException.ThrowIfNull(callback);

        if (size <= bucket)
        {
            if (size > 0)
                callback(0, size);
            return;
        }

        var offset = -checked((int)bucket);
        var maxThreads = Math.Max(1u, workQueue.GetNumProcessingThreads());
        for (var i = 0u; i < maxThreads; ++i)
        {
            workQueue.PostTask((_, _) =>
            {
                while (true)
                {
                    var beginIndex = (uint)(Interlocked.Add(ref offset, checked((int)bucket)));
                    if (beginIndex >= size)
                        break;

                    var endIndex = Math.Min(beginIndex + bucket, size);
                    callback(beginIndex, endIndex);
                }
            }, TaskPriority.Immediate);
        }
        workQueue.CompleteImmediateForThisThread();
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, uint bucket, IReadOnlyList<T> collection, Action<uint, T> callback)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(callback);

        var collectionSize = (uint)collection.Count;
        ForEachParallel(workQueue, bucket, collectionSize, (beginIndex, endIndex) =>
        {
            for (var index = beginIndex; index < endIndex; ++index)
                callback(index, collection[checked((int)index)]);
        });
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, IReadOnlyList<T> collection, Action<uint, T> callback)
    {
        ForEachParallel(workQueue, 1u, collection, callback);
    }

    [Obsolete("Use PostTaskForMainThread instead.")]
    public void CallFromMainThread(Action<uint> workFunction)
    {
        PostTaskForMainThread(_ => workFunction(0), TaskPriority.Immediate);
    }

    [Obsolete("Use CompleteImmediateForThisThread or CompleteAll instead.")]
    public void Complete(uint priority)
    {
        if (priority == uint.MaxValue)
            CompleteImmediateForThisThread();
        else
            CompleteAll();
    }

    [Obsolete("GetFreeItem is deprecated.")]
    public WorkItem GetFreeItem()
    {
        return new WorkItem();
    }

    [Obsolete("AddWorkItem is deprecated. Use PostTask instead.")]
    public void AddWorkItem(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.WorkFunction == null)
        {
            Log.Logger.LogError("Null work item submitted to the work queue");
            return;
        }

        var priority = ConvertLegacyPriority(item.Priority);
        PostTask((int threadIndex) => item.WorkFunction(item, threadIndex), priority);
    }

    [Obsolete("AddWorkItem is deprecated. Use PostTask instead.")]
    public WorkItem AddWorkItem(Action<uint> workFunction, uint priority = 0)
    {
        var item = GetFreeItem();
        item.WorkLambda = ti => workFunction((uint)ti);
        item.WorkFunction = (workItem, threadIndex) => workItem.WorkLambda?.Invoke((uint)threadIndex);
        item.Priority = priority;
        AddWorkItem(item);
        return item;
    }

    private uint GetThreadIndexImpl()
    {
        if (_numProcessingThreads <= 1)
            return Thread.CurrentThread.ManagedThreadId == GetMainThreadId() ? 0u : uint.MaxValue;

        return Thread.CurrentThread.ManagedThreadId == GetMainThreadId()
            ? 0u
            : (uint)(Math.Abs(Thread.CurrentThread.ManagedThreadId) % (int)_numProcessingThreads);
    }

    private void ProcessPostedTasks()
    {
        if (_fallbackTaskQueue.Count > 0)
        {
            var timer = Stopwatch.StartNew();
            var budget = _maxNonThreadedWorkMs * 1000L;
            for (var i = 0; i < _fallbackTaskQueue.Count; ++i)
            {
                if (timer.ElapsedTicks * 1_000_000L / Stopwatch.Frequency >= budget)
                    break;

                var task = _fallbackTaskQueue[i].Task;
                if (task != null)
                {
                    task(0, this);
                    _fallbackTaskQueue[i] = (_fallbackTaskQueue[i].Priority, null);
                }
            }

            _fallbackTaskQueue.RemoveAll(static t => t.Task == null);
        }
    }

    private bool ProcessMainThreadTasks()
    {
        lock (_mainThreadTasksLock)
        {
            (_mainThreadTasks, _mainThreadTasksSwap) = (_mainThreadTasksSwap, _mainThreadTasks);
        }

        foreach (var callback in _mainThreadTasksSwap)
            callback(0, this);

        var nothingToRun = _mainThreadTasksSwap.Count == 0;
        _mainThreadTasksSwap.Clear();

        return !nothingToRun;
    }

    private void EnsurePendingImmediateSize(int size)
    {
        while (_pendingImmediateTasks.Count < size)
        {
            lock (_pendingImmediateTasks)
            {
                while (_pendingImmediateTasks.Count < size)
                    _pendingImmediateTasks.Add([]);
            }
        }
    }

    private static TaskPriority ConvertLegacyPriority(uint priority)
    {
        return priority == uint.MaxValue
            ? TaskPriority.Immediate
            : (TaskPriority)((uint)TaskPriority.Low - Math.Min(priority, 2u));
    }

    private static int _mainThreadId = -1;

    internal static void SetMainThread()
    {
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    internal static int GetMainThreadId() => _mainThreadId;
}
