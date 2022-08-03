using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Threading;

[DebuggerDisplay("Signaled: {IsSet}")]
public class AsyncManualResetEvent
{
    /// <summary>
    /// Whether the task completion source should allow executing continuations synchronously.
    /// </summary>
    private readonly bool allowInliningAwaiters;
    /// <summary>The object to lock when accessing fields.</summary>
    private readonly object syncObject = new object();
    /// <summary>
    /// The source of the task to return from <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" />.
    /// </summary>
    /// <devremarks>
    /// This should not need the volatile modifier because it is
    /// always accessed within a lock.
    /// </devremarks>
    private TaskCompletionSourceWithoutInlining<EmptyStruct> taskCompletionSource;
    /// <summary>
    /// A flag indicating whether the event is signaled.
    /// When this is set to true, it's possible that
    /// <see cref="F:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.taskCompletionSource" />.Task.IsCompleted is still false
    /// if the completion has been scheduled asynchronously.
    /// Thus, this field should be the definitive answer as to whether
    /// the event is signaled because it is synchronously updated.
    /// </summary>
    /// <devremarks>
    /// This should not need the volatile modifier because it is
    /// always accessed within a lock.
    /// </devremarks>
    private bool isSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:Microsoft.VisualStudio.Threading.AsyncManualResetEvent" /> class.
    /// </summary>
    /// <param name="initialState">A value indicating whether the event should be initially signaled.</param>
    /// <param name="allowInliningAwaiters">
    /// A value indicating whether to allow <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" /> callers' continuations to execute
    /// on the thread that calls <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.SetAsync" /> before the call returns.
    /// <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.SetAsync" /> callers should not hold private locks if this value is <c>true</c> to avoid deadlocks.
    /// When <c>false</c>, the task returned from <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" /> may not have fully transitioned to
    /// its completed state by the time <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.SetAsync" /> returns to its caller.
    /// </param>
    public AsyncManualResetEvent(bool initialState = false, bool allowInliningAwaiters = false)
    {
        this.allowInliningAwaiters = allowInliningAwaiters;
        this.taskCompletionSource = this.CreateTaskSource();
        this.isSet = initialState;
        if (!initialState)
            return;
        this.taskCompletionSource.SetResult(EmptyStruct.Instance);
    }

    /// <summary>
    /// Gets a value indicating whether the event is currently in a signaled state.
    /// </summary>
    public bool IsSet
    {
        get
        {
            lock (this.syncObject)
                return this.isSet;
        }
    }

    /// <summary>
    /// Returns a task that will be completed when this event is set.
    /// </summary>
    public Task WaitAsync()
    {
        lock (this.syncObject)
            return (Task)this.taskCompletionSource.Task;
    }

    /// <summary>
    /// Returns a task that will be completed when this event is set.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the event is set, or cancels with the <paramref name="cancellationToken" />.</returns>
    public Task WaitAsync(CancellationToken cancellationToken) => this.WaitAsync().WithCancellation(cancellationToken);

    /// <summary>
    /// Sets this event to unblock callers of <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" />.
    /// </summary>
    /// <returns>A task that completes when the signal has been set.</returns>
    /// <remarks>
    /// <para>
    /// On .NET versions prior to 4.6:
    /// This method may return before the signal set has propagated (so <see cref="P:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.IsSet" /> may return <c>false</c> for a bit more if called immediately).
    /// The returned task completes when the signal has definitely been set.
    /// </para>
    /// <para>
    /// On .NET 4.6 and later:
    /// This method is not asynchronous. The returned Task is always completed.
    /// </para>
    /// </remarks>
    [Obsolete("Use Set() instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task SetAsync()
    {
        TaskCompletionSourceWithoutInlining<EmptyStruct> sourceWithoutInlining = (TaskCompletionSourceWithoutInlining<EmptyStruct>)null;
        bool flag = false;
        lock (this.syncObject)
        {
            flag = !this.isSet;
            sourceWithoutInlining = this.taskCompletionSource;
            this.isSet = true;
        }
        Task<EmptyStruct> task = sourceWithoutInlining.Task;
        if (!flag)
            return (Task)task;
        sourceWithoutInlining.TrySetResult(new EmptyStruct());
        return (Task)task;
    }

    /// <summary>
    /// Sets this event to unblock callers of <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" />.
    /// </summary>
    public void Set() => this.SetAsync();

    /// <summary>
    /// Resets this event to a state that will block callers of <see cref="M:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.WaitAsync" />.
    /// </summary>
    public void Reset()
    {
        lock (this.syncObject)
        {
            if (!this.isSet)
                return;
            this.taskCompletionSource = this.CreateTaskSource();
            this.isSet = false;
        }
    }

    /// <summary>
    /// Sets and immediately resets this event, allowing all current waiters to unblock.
    /// </summary>
    /// <returns>A task that completes when the signal has been set.</returns>
    /// <remarks>
    /// <para>
    /// On .NET versions prior to 4.6:
    /// This method may return before the signal set has propagated (so <see cref="P:Microsoft.VisualStudio.Threading.AsyncManualResetEvent.IsSet" /> may return <c>false</c> for a bit more if called immediately).
    /// The returned task completes when the signal has definitely been set.
    /// </para>
    /// <para>
    /// On .NET 4.6 and later:
    /// This method is not asynchronous. The returned Task is always completed.
    /// </para>
    /// </remarks>
    [Obsolete("Use PulseAll() instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task PulseAllAsync()
    {
        TaskCompletionSourceWithoutInlining<EmptyStruct> sourceWithoutInlining = (TaskCompletionSourceWithoutInlining<EmptyStruct>)null;
        lock (this.syncObject)
        {
            sourceWithoutInlining = this.taskCompletionSource;
            this.taskCompletionSource = this.CreateTaskSource();
            this.isSet = false;
        }
        Task task = (Task)sourceWithoutInlining.Task;
        sourceWithoutInlining.TrySetResult(new EmptyStruct());
        return task;
    }

    /// <summary>
    /// Sets and immediately resets this event, allowing all current waiters to unblock.
    /// </summary>
    public void PulseAll() => this.PulseAllAsync();

    /// <summary>
    /// Gets an awaiter that completes when this event is signaled.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TaskAwaiter GetAwaiter() => this.WaitAsync().GetAwaiter();

    /// <summary>
    /// Creates a new TaskCompletionSource to represent an unset event.
    /// </summary>
    private TaskCompletionSourceWithoutInlining<EmptyStruct> CreateTaskSource() => new TaskCompletionSourceWithoutInlining<EmptyStruct>(this.allowInliningAwaiters);
}