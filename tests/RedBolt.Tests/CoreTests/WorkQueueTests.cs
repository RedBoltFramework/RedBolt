using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class WorkQueueTests
{
    private readonly HeadlessEngineFixture fixture;

    public WorkQueueTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private WorkQueue GetWorkQueue() =>
        fixture.Context.GetSubsystem<WorkQueue>()
            ?? throw new InvalidOperationException("WorkQueue is not registered in Context.");

    [Fact]
    public void WorkQueue_is_registered_as_subsystem()
    {
        var workQueue = GetWorkQueue();
        Assert.NotNull(workQueue);
        Assert.IsType<WorkQueue>(workQueue);
        Assert.Same(fixture.Engine.Context, workQueue.Context);
    }

    [Fact]
    public void TaskPriority_enum_values_are_correct()
    {
        Assert.Equal(0, (int)TaskPriority.Immediate);
        Assert.Equal(1, (int)TaskPriority.Highest);
        Assert.Equal(2, (int)TaskPriority.High);
        Assert.Equal(3, (int)TaskPriority.Medium);
        Assert.Equal(4, (int)TaskPriority.Low);
    }

    [Fact]
    public void PostTask_executes_immediate_task()
    {
        var workQueue = GetWorkQueue();
        var executed = false;
        workQueue.PostTask(() => executed = true, TaskPriority.Immediate);
        Assert.True(executed);
    }

    [Fact]
    public void PostTask_executes_with_threadIndex_and_queue()
    {
        var workQueue = GetWorkQueue();
        var receivedThreadIndex = -1;
        WorkQueue? receivedQueue = null;

        workQueue.PostTask((threadIndex, queue) =>
        {
            receivedThreadIndex = threadIndex;
            receivedQueue = queue;
        }, TaskPriority.Immediate);

        Assert.Equal(0, receivedThreadIndex);
        Assert.Same(workQueue, receivedQueue);
    }

    [Fact]
    public void PostTask_executes_with_threadIndex_only()
    {
        var workQueue = GetWorkQueue();
        var receivedThreadIndex = -1;

        workQueue.PostTask((int threadIndex) => receivedThreadIndex = threadIndex, TaskPriority.Immediate);

        Assert.Equal(0, receivedThreadIndex);
    }

    [Fact]
    public void PostTask_executes_with_action()
    {
        var workQueue = GetWorkQueue();
        var executed = false;
        workQueue.PostTask(() => executed = true, TaskPriority.Immediate);
        Assert.True(executed);
    }

    [Fact]
    public void PostTaskForMainThread_executes_on_main_thread()
    {
        var workQueue = GetWorkQueue();
        var executed = false;

        workQueue.PostTaskForMainThread(() => executed = true, TaskPriority.Immediate);

        Assert.True(executed);
    }

    [Fact]
    public void PostTaskForMainThread_with_threadIndex_runs_immediately_when_main_thread()
    {
        var workQueue = GetWorkQueue();
        var receivedThreadIndex = -1;

        workQueue.PostTaskForMainThread((int threadIndex) => receivedThreadIndex = threadIndex, TaskPriority.Immediate);

        Assert.Equal(0, receivedThreadIndex);
    }

    [Fact]
    public void PostDelayedTaskForMainThread_does_not_execute_until_complete()
    {
        var workQueue = GetWorkQueue();
        var executed = false;

        workQueue.PostDelayedTaskForMainThread(() => executed = true);

        Assert.False(executed);
        workQueue.CompleteAll();
        Assert.True(executed);
    }

    [Fact]
    public void CompleteAll_processes_all_pending_tasks()
    {
        var workQueue = GetWorkQueue();
        var counter = 0;

        for (var i = 0; i < 5; ++i)
            workQueue.PostTask(() => Interlocked.Increment(ref counter), TaskPriority.Immediate);

        Assert.Equal(5, counter);
        Assert.True(workQueue.IsCompleted());
    }

    [Fact]
    public void GetNumIncomplete_returns_zero_when_no_tasks()
    {
        var workQueue = GetWorkQueue();
        Assert.Equal(0u, workQueue.GetNumIncomplete());
    }

    [Fact]
    public void IsCompleted_returns_true_when_no_tasks()
    {
        var workQueue = GetWorkQueue();
        Assert.True(workQueue.IsCompleted());
    }

    [Fact]
    public void GetNumIncomplete_reflects_fallback_tasks()
    {
        var workQueue = GetWorkQueue();
        // Post a non-immediate task which goes to fallback queue when non-threaded
        workQueue.PostTask(() => { }, TaskPriority.High);

        Assert.True(workQueue.GetNumIncomplete() > 0);
        Assert.False(workQueue.IsCompleted());

        workQueue.CompleteAll();
        Assert.Equal(0u, workQueue.GetNumIncomplete());
        Assert.True(workQueue.IsCompleted());
    }

    [Fact]
    public void GetThreadIndex_returns_zero_for_main_thread()
    {
        Assert.Equal(0u, WorkQueue.GetThreadIndex());
    }

    [Fact]
    public void GetThreadIndexCount_returns_positive_value()
    {
        Assert.True(WorkQueue.GetThreadIndexCount() >= 1);
    }

    [Fact]
    public void IsProcessingThread_returns_true_for_main_thread()
    {
        Assert.True(WorkQueue.IsProcessingThread());
    }

    [Fact]
    public void Initialize_called_with_zero_threads()
    {
        var workQueue = GetWorkQueue();
        workQueue.Initialize(0);

        Assert.False(workQueue.IsMultithreaded());
        Assert.Equal(1u, workQueue.GetNumProcessingThreads());
    }

    [Fact]
    public void Initialize_called_multiple_times_is_idempotent()
    {
        var workQueue = GetWorkQueue();
        workQueue.Initialize(0);
        var threadsBefore = workQueue.GetNumProcessingThreads();
        workQueue.Initialize(4);
        Assert.Equal(threadsBefore, workQueue.GetNumProcessingThreads());
    }

    [Fact]
    public void SetNonThreadedWorkMs_and_GetNonThreadedWorkMs_roundtrip()
    {
        var workQueue = GetWorkQueue();
        workQueue.SetNonThreadedWorkMs(10);
        Assert.Equal(10, workQueue.GetNonThreadedWorkMs());
    }

    [Fact]
    public void PostTask_executes_in_posted_order()
    {
        var workQueue = GetWorkQueue();
        var results = new List<int>();

        workQueue.PostTask(() => results.Add(1), TaskPriority.Immediate);
        workQueue.PostTask(() => results.Add(2), TaskPriority.Immediate);
        workQueue.PostTask(() => results.Add(3), TaskPriority.Immediate);

        Assert.Equal([1, 2, 3], results);
    }

    [Fact]
    public void PostDelayedTaskForMainThread_preserves_post_order()
    {
        var workQueue = GetWorkQueue();
        var results = new List<int>();

        workQueue.PostDelayedTaskForMainThread(() => results.Add(1));
        workQueue.PostDelayedTaskForMainThread(() => results.Add(2));
        workQueue.PostDelayedTaskForMainThread(() => results.Add(3));

        Assert.Empty(results);
        workQueue.CompleteAll();

        Assert.Equal([1, 2, 3], results);
    }

    [Fact]
    public void CallFromMainThread_executes_from_main_thread()
    {
        var workQueue = GetWorkQueue();
        var executed = false;

#pragma warning disable CS0612
        workQueue.CallFromMainThread(_ => executed = true);
#pragma warning restore CS0612

        Assert.True(executed);
    }

    [Fact]
    public void Complete_with_max_priority_does_not_throw()
    {
        var workQueue = GetWorkQueue();

#pragma warning disable CS0612
        workQueue.Complete(uint.MaxValue);
        workQueue.Complete(0);
#pragma warning restore CS0612

        Assert.True(workQueue.IsCompleted());
    }

    [Fact]
    public void GetFreeItem_returns_new_work_item()
    {
        var workQueue = GetWorkQueue();

#pragma warning disable CS0612
        var item = workQueue.GetFreeItem();
#pragma warning restore CS0612

        Assert.NotNull(item);
    }

    [Fact]
    public void AddWorkItem_executes_work_function()
    {
        var workQueue = GetWorkQueue();
        var executed = false;

#pragma warning disable CS0612
        workQueue.AddWorkItem(ti =>
        {
            Assert.Equal(0u, ti);
            executed = true;
        });
#pragma warning restore CS0612

        workQueue.CompleteAll();
        Assert.True(executed);
    }

    [Fact]
    public void AddWorkItem_with_explicit_workitem_calls_work_function()
    {
        var workQueue = GetWorkQueue();
        var called = false;
        var item = new WorkItem();
        item.WorkFunction = (wi, threadIndex) =>
        {
            Assert.Same(item, wi);
            Assert.Equal(0, threadIndex);
            called = true;
        };

#pragma warning disable CS0612
        workQueue.AddWorkItem(item);
#pragma warning restore CS0612

        workQueue.CompleteAll();
        Assert.True(called);
    }

    [Fact]
    public void GetNumProcessingThreads_is_zero_before_initialize()
    {
        var workQueue = GetWorkQueue();

        Assert.Equal(0u, workQueue.GetNumProcessingThreads());
        Assert.False(workQueue.IsMultithreaded());
    }
}
