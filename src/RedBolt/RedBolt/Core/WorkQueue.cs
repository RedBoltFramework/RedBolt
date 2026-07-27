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

public sealed class WorkQueueVector<T> : MultiVector<T>
{
    public void Clear() => base.Clear(WorkQueue.GetThreadIndexCount());

    public MultiVector<T>.Index Insert(T value) => PushBack(WorkQueue.GetThreadIndex(), value);

    public ref T Emplace(Func<T> factory) => ref EmplaceBack(WorkQueue.GetThreadIndex(), factory);
}

[Obsolete("WorkItem is deprecated. Use TaskFunctionDelegate instead.")]
public sealed class WorkItem
{
    public Action<WorkItem, int>? WorkFunction;
    public IntPtr Start;
    public IntPtr End;
    public IntPtr Aux;
    public uint Priority;
    internal Action<int>? WorkLambda;
}

public sealed class WorkQueue : RedObject
{
    private sealed class ParallelForState
    {
        public ParallelForState()
        {
            Task = Run;
        }

        public readonly TaskFunctionDelegate Task;
        public int Offset;
        public int Bucket;
        public int Size;
        public Action<int, int>? Callback;

        private void Run(int threadIndex, WorkQueue queue)
        {
            while (true)
            {
                var beginIndex = Interlocked.Add(ref Offset, Bucket);
                if (beginIndex >= Size)
                    break;

                var endIndex = Math.Min(beginIndex + Bucket, Size);
                Callback!(beginIndex, endIndex);
            }
        }
    }

    private static class ParallelForStateCache
    {
        [ThreadStatic]
        public static List<ParallelForState>? States;

        [ThreadStatic]
        public static int Depth;
    }

    private sealed class ReadOnlyListParallelForState<T>
    {
        public ReadOnlyListParallelForState()
        {
            RangeCallback = Run;
        }

        public readonly Action<int, int> RangeCallback;
        public IReadOnlyList<T>? Collection;
        public Action<int, T>? Callback;

        private void Run(int beginIndex, int endIndex)
        {
            for (var index = beginIndex; index < endIndex; ++index)
                Callback!(index, Collection![index]);
        }
    }

    private static class ReadOnlyListParallelForStateCache<T>
    {
        [ThreadStatic]
        public static List<ReadOnlyListParallelForState<T>>? States;

        [ThreadStatic]
        public static int Depth;
    }

    private sealed class MultiVectorParallelForState<T>
    {
        public MultiVectorParallelForState()
        {
            RangeCallback = Run;
        }

        public readonly Action<int, int> RangeCallback;
        public List<List<T>>? Collection;
        public Action<int, T>? Callback;

        private void Run(int beginIndex, int endIndex)
        {
            for (var index = beginIndex; index < endIndex; ++index)
            {
                var innerIndex = index;
                for (var outerIndex = 0; outerIndex < Collection!.Count; ++outerIndex)
                {
                    var innerCollection = Collection[outerIndex];
                    if (innerIndex < innerCollection.Count)
                    {
                        Callback!(index, innerCollection[innerIndex]);
                        break;
                    }
                    innerIndex -= innerCollection.Count;
                }
            }
        }
    }

    private static class MultiVectorParallelForStateCache<T>
    {
        [ThreadStatic]
        public static List<MultiVectorParallelForState<T>>? States;

        [ThreadStatic]
        public static int Depth;
    }

    private static int _threadIndexCount = 1;
    private static WorkQueue? _workQueue;

    private int _numProcessingThreads;

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
    }

    public override void Dispose()
    {
        while (Interlocked.CompareExchange(ref _pendingTaskCount, 0, 0) > 0)
            Thread.Sleep(0);

        if (ReferenceEquals(_workQueue, this))
        {
            _threadIndexCount = 0;
            _workQueue = null;
        }

        for (var i = _pendingImmediateTasks.Count - 1; i >= 0; --i)
            _pendingImmediateTasks[i].Clear();
        _pendingImmediateTasks.Clear();
        lock (_mainThreadTasksLock)
        {
            _mainThreadTasksSwap.Clear();
            _mainThreadTasks.Clear();
        }
        _fallbackTaskQueue.Clear();

        base.Dispose();
    }

    public void Initialize(int numThreads)
    {
        if (_numProcessingThreads > 0)
            return;

        _numProcessingThreads = numThreads + 1;
        _threadIndexCount = 1;

        if (numThreads > 0)
        {
            ThreadPool.SetMinThreads(_numProcessingThreads, _numProcessingThreads);

            for (var i = 1; i < _numProcessingThreads; ++i)
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
            PostTaskFromExternalThread(task, priority);
            return;
        }

        if (_numProcessingThreads > 1)
        {
            if (priority == TaskPriority.Immediate)
            {
                var threadIndex = GetThreadIndex();
                EnsurePendingImmediateSize(threadIndex + 1);
                _pendingImmediateTasks[threadIndex].Add(task);
            }
            else
            {
                PostBackgroundTask(task);
            }
            return;
        }

        if (priority == TaskPriority.Immediate)
            task(0, this);
        else
            _fallbackTaskQueue.Add((priority, task));
    }

    private void PostTaskFromExternalThread(TaskFunctionDelegate task, TaskPriority priority)
    {
        var capturedTask = task;
        var capturedPriority = priority;
        TaskFunctionDelegate wrapped = (_, queue) => queue.PostTask(capturedTask, capturedPriority);
        PostTaskForMainThread(wrapped, priority);
    }

    private void PostBackgroundTask(TaskFunctionDelegate task)
    {
        Interlocked.Increment(ref _pendingTaskCount);
        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                task(GetThreadIndex(), this);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingTaskCount);
            }
        }, null);
    }

    public void PostTaskForThread(Action<int> task, TaskPriority priority, int threadIndex)
    {
        PostTaskForThread((threadIdx, _) => task(threadIdx), priority, threadIndex);
    }

    public void PostTaskForThread(Action task, TaskPriority priority, int threadIndex)
    {
        PostTaskForThread((_, _) => task(), priority, threadIndex);
    }

    public void PostTaskForThread(TaskFunctionDelegate task, TaskPriority priority, int threadIndex)
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
                    task(GetThreadIndex(), this);
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
            var threadIndex = GetThreadIndex();
            EnsurePendingImmediateSize(threadIndex + 1);
            var pending = _pendingImmediateTasks[threadIndex];
            if (pending.Count > 0)
            {
                var taskCount = pending.Count;
                for (var i = 0; i < taskCount; ++i)
                    pending[i](threadIndex, this);
                pending.Clear();
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

    public int GetNumIncomplete()
    {
        var result = _fallbackTaskQueue.Count;
        result += Volatile.Read(ref _pendingTaskCount);
        foreach (var pending in _pendingImmediateTasks)
            result += pending.Count;
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

    public int GetNumProcessingThreads()
    {
        return _numProcessingThreads;
    }

    public bool IsMultithreaded()
    {
        return _numProcessingThreads > 1;
    }

    public static int GetThreadIndex()
    {
        return _workQueue?.GetThreadIndexImpl() ?? 0;
    }

    public static int GetThreadIndexCount()
    {
        return _threadIndexCount;
    }

    public static bool IsProcessingThread()
    {
        return GetThreadIndex() < _threadIndexCount;
    }

    public static void ForEachParallel(WorkQueue workQueue, int bucket, int size, Action<int, int> callback)
    {
        ArgumentNullException.ThrowIfNull(workQueue);
        ArgumentNullException.ThrowIfNull(callback);

        if (size <= bucket)
        {
            if (size > 0)
                callback(0, size);
            return;
        }

        var states = ParallelForStateCache.States ??= [];
        var stateIndex = ParallelForStateCache.Depth++;
        if (stateIndex == states.Count)
            states.Add(new ParallelForState());
        var state = states[stateIndex];
        state.Offset = -bucket;
        state.Bucket = bucket;
        state.Size = size;
        state.Callback = callback;
        try
        {
            var maxThreads = Math.Max(1, workQueue.GetNumProcessingThreads());
            for (var i = 0; i < maxThreads; ++i)
                workQueue.PostTask(state.Task, TaskPriority.Immediate);
            workQueue.CompleteImmediateForThisThread();
        }
        finally
        {
            state.Callback = null;
            --ParallelForStateCache.Depth;
        }
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, int bucket, IReadOnlyList<T> collection, Action<int, T> callback)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(callback);

        var states = ReadOnlyListParallelForStateCache<T>.States ??= [];
        var stateIndex = ReadOnlyListParallelForStateCache<T>.Depth++;
        if (stateIndex == states.Count)
            states.Add(new ReadOnlyListParallelForState<T>());
        var state = states[stateIndex];
        state.Collection = collection;
        state.Callback = callback;
        try
        {
            ForEachParallel(workQueue, bucket, collection.Count, state.RangeCallback);
        }
        finally
        {
            state.Collection = null;
            state.Callback = null;
            --ReadOnlyListParallelForStateCache<T>.Depth;
        }
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, IReadOnlyList<T> collection, Action<int, T> callback)
    {
        ForEachParallel(workQueue, 1, collection, callback);
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, int bucket, MultiVector<T> collection, Action<int, T> callback)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(callback);

        var states = MultiVectorParallelForStateCache<T>.States ??= [];
        var stateIndex = MultiVectorParallelForStateCache<T>.Depth++;
        if (stateIndex == states.Count)
            states.Add(new MultiVectorParallelForState<T>());
        var state = states[stateIndex];
        state.Collection = collection.GetUnderlyingCollection();
        state.Callback = callback;
        try
        {
            ForEachParallel(workQueue, bucket, collection.Size(), state.RangeCallback);
        }
        finally
        {
            state.Collection = null;
            state.Callback = null;
            --MultiVectorParallelForStateCache<T>.Depth;
        }
    }

    public static void ForEachParallel<T>(WorkQueue workQueue, MultiVector<T> collection, Action<int, T> callback)
    {
        ForEachParallel(workQueue, 1, collection, callback);
    }

    [Obsolete("Use PostTaskForMainThread instead.")]
    public void CallFromMainThread(Action<int> workFunction)
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
            Log.GetLogger().LogError("Null work item submitted to the work queue");
            return;
        }

        var priority = ConvertLegacyPriority(item.Priority);
        PostTask((int threadIndex) => item.WorkFunction(item, threadIndex), priority);
    }

    [Obsolete("AddWorkItem is deprecated. Use PostTask instead.")]
    public WorkItem AddWorkItem(Action<int> workFunction, uint priority = 0)
    {
        var item = GetFreeItem();
        item.WorkLambda = workFunction;
        item.WorkFunction = (workItem, threadIndex) => workItem.WorkLambda?.Invoke(threadIndex);
        item.Priority = priority;
        AddWorkItem(item);
        return item;
    }

    private int GetThreadIndexImpl()
    {
        if (_numProcessingThreads <= 1)
            return Thread.CurrentThread.ManagedThreadId == GetMainThreadId() ? 0 : int.MaxValue;

        return Thread.CurrentThread.ManagedThreadId == GetMainThreadId()
            ? 0
            : Math.Abs(Thread.CurrentThread.ManagedThreadId) % _numProcessingThreads;
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
