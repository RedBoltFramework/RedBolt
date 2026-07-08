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
    private static uint threadIndexCount = 1;
    private static WorkQueue? workQueue;

    private uint numProcessingThreads;

    private readonly List<(TaskPriority Priority, TaskFunctionDelegate? Task)> fallbackTaskQueue = [];

    private List<TaskFunctionDelegate> mainThreadTasks = [];
    private List<TaskFunctionDelegate> mainThreadTasksSwap = [];
    private readonly object mainThreadTasksLock = new();

    private int maxNonThreadedWorkMs = 5;

    private int pendingTaskCount;

    private List<List<TaskFunctionDelegate>> pendingImmediateTasks = [];

    public WorkQueue()
    {
        if (workQueue == null)
        {
            threadIndexCount = 1;
            workQueue = this;
        }

        SubscribeToEvent<CoreEvents.BeginFrameEvent>((ref CoreEvents.BeginFrameEvent _) => Update());
    }

    public void Initialize(uint numThreads)
    {
        if (numProcessingThreads > 0)
            return;

        numProcessingThreads = numThreads + 1;
        threadIndexCount = 1;

        if (numThreads > 0)
        {
            ThreadPool.SetMinThreads((int)numProcessingThreads, (int)numProcessingThreads);

            for (var i = 1u; i < numProcessingThreads; ++i)
            {
                PostTaskForThread(_ => { }, TaskPriority.Immediate, i);
            }

            CompleteAll();

            threadIndexCount = numProcessingThreads;
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

        if (numProcessingThreads > 1)
        {
            if (priority == TaskPriority.Immediate)
            {
                var threadIndex = (int)GetThreadIndex();
                EnsurePendingImmediateSize(threadIndex + 1);
                pendingImmediateTasks[threadIndex].Add(task);
            }
            else
            {
                Interlocked.Increment(ref pendingTaskCount);
                ThreadPool.UnsafeQueueUserWorkItem(_ =>
                {
                    try
                    {
                        task((int)GetThreadIndex(), this);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref pendingTaskCount);
                    }
                }, null);
            }
            return;
        }

        if (priority == TaskPriority.Immediate)
            task(0, this);
        else
            fallbackTaskQueue.Add((priority, task));
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

        if (numProcessingThreads > 1)
        {
            Interlocked.Increment(ref pendingTaskCount);
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    task((int)GetThreadIndex(), this);
                }
                finally
                {
                    Interlocked.Decrement(ref pendingTaskCount);
                }
            }, null);
            return;
        }

        if (priority == TaskPriority.Immediate)
            task(0, this);
        else
            fallbackTaskQueue.Add((priority, task));
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
        if (numProcessingThreads > 1 && IsProcessingThread())
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
        lock (mainThreadTasksLock)
        {
            mainThreadTasks.Add(task);
        }
    }

    public void CompleteImmediateForThisThread()
    {
        if (numProcessingThreads > 1)
        {
            var threadIndex = (int)GetThreadIndex();
            EnsurePendingImmediateSize(threadIndex + 1);
            var pending = pendingImmediateTasks[threadIndex];
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

        while (Interlocked.CompareExchange(ref pendingTaskCount, 0, 0) > 0)
        {
            Thread.Sleep(0);
        }

        if (fallbackTaskQueue.Count > 0)
        {
            foreach (var (_, task) in fallbackTaskQueue)
                task?.Invoke(0, this);
            fallbackTaskQueue.Clear();
        }

        while (ProcessMainThreadTasks())
        {
        }
    }

    public uint GetNumIncomplete()
    {
        var result = (uint)fallbackTaskQueue.Count;
        result += (uint)Volatile.Read(ref pendingTaskCount);
        foreach (var pending in pendingImmediateTasks)
            result += (uint)pending.Count;
        return result;
    }

    public bool IsCompleted()
    {
        return GetNumIncomplete() == 0;
    }

    public void SetNonThreadedWorkMs(int ms)
    {
        maxNonThreadedWorkMs = Math.Max(ms, 1);
    }

    public int GetNonThreadedWorkMs()
    {
        return maxNonThreadedWorkMs;
    }

    public uint GetNumProcessingThreads()
    {
        return numProcessingThreads;
    }

    public bool IsMultithreaded()
    {
        return numProcessingThreads > 1;
    }

    public static uint GetThreadIndex()
    {
        return workQueue?.GetThreadIndexImpl() ?? 0;
    }

    public static uint GetThreadIndexCount()
    {
        return threadIndexCount;
    }

    public static bool IsProcessingThread()
    {
        return GetThreadIndex() < threadIndexCount;
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
        if (numProcessingThreads <= 1)
            return Thread.CurrentThread.ManagedThreadId == GetMainThreadId() ? 0u : uint.MaxValue;

        return Thread.CurrentThread.ManagedThreadId == GetMainThreadId()
            ? 0u
            : (uint)(Math.Abs(Thread.CurrentThread.ManagedThreadId) % (int)numProcessingThreads);
    }

    private void ProcessPostedTasks()
    {
        if (fallbackTaskQueue.Count > 0)
        {
            var timer = Stopwatch.StartNew();
            var budget = maxNonThreadedWorkMs * 1000L;
            for (var i = 0; i < fallbackTaskQueue.Count; ++i)
            {
                if (timer.ElapsedTicks * 1_000_000L / Stopwatch.Frequency >= budget)
                    break;

                var task = fallbackTaskQueue[i].Task;
                if (task != null)
                {
                    task(0, this);
                    fallbackTaskQueue[i] = (fallbackTaskQueue[i].Priority, null);
                }
            }

            fallbackTaskQueue.RemoveAll(static t => t.Task == null);
        }
    }

    private bool ProcessMainThreadTasks()
    {
        lock (mainThreadTasksLock)
        {
            (mainThreadTasks, mainThreadTasksSwap) = (mainThreadTasksSwap, mainThreadTasks);
        }

        foreach (var callback in mainThreadTasksSwap)
            callback(0, this);

        var nothingToRun = mainThreadTasksSwap.Count == 0;
        mainThreadTasksSwap.Clear();

        return !nothingToRun;
    }

    private void EnsurePendingImmediateSize(int size)
    {
        while (pendingImmediateTasks.Count < size)
        {
            lock (pendingImmediateTasks)
            {
                while (pendingImmediateTasks.Count < size)
                    pendingImmediateTasks.Add([]);
            }
        }
    }

    private static TaskPriority ConvertLegacyPriority(uint priority)
    {
        return priority == uint.MaxValue
            ? TaskPriority.Immediate
            : (TaskPriority)((uint)TaskPriority.Low - Math.Min(priority, 2u));
    }

    private static int mainThreadId = -1;

    internal static void SetMainThread()
    {
        mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }

    internal static int GetMainThreadId() => mainThreadId;
}
