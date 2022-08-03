﻿using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Threading;

internal class TaskCompletionSourceWithoutInlining<T> : TaskCompletionSource<T>
{
    /// <summary>
    /// The Task that we expose to others that may not inline continuations.
    /// </summary>
    private readonly Task<T> exposedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskCompletionSourceWithoutInlining{T}"/> class.
    /// </summary>
    /// <param name="allowInliningContinuations">
    /// <c>true</c> to allow continuations to be inlined; otherwise <c>false</c>.
    /// </param>
    /// <param name="options">
    /// TaskCreationOptions to pass on to the base constructor.
    /// </param>
    /// <param name="state">The state to set on the Task.</param>
    internal TaskCompletionSourceWithoutInlining(bool allowInliningContinuations, TaskCreationOptions options = TaskCreationOptions.None, object? state = null)
        : base(state, AdjustFlags(options, allowInliningContinuations))
    {
        this.exposedTask = base.Task;
    }

    /// <summary>
    /// Gets the <see cref="Task"/> that may never complete inline with completion of this <see cref="TaskCompletionSource{TResult}"/>.
    /// </summary>
    /// <devremarks>
    /// Return the base.Task if it is already completed since inlining continuations
    /// on the completer is no longer a concern. Also, when we are not inlining continuations,
    /// this.exposedTask completes slightly later than base.Task, and callers expect
    /// the Task we return to be complete as soon as they call TrySetResult.
    /// </devremarks>
    internal new Task<T> Task => base.Task.IsCompleted ? base.Task : this.exposedTask;

    /// <summary>
    /// Modifies the specified flags to include RunContinuationsAsynchronously
    /// if wanted by the caller and supported by the platform.
    /// </summary>
    /// <param name="options">The base options supplied by the caller.</param>
    /// <param name="allowInliningContinuations"><c>true</c> to allow inlining continuations.</param>
    /// <returns>The possibly modified flags.</returns>
    private static TaskCreationOptions AdjustFlags(TaskCreationOptions options, bool allowInliningContinuations)
    {
        return allowInliningContinuations
            ? (options & ~TaskCreationOptions.RunContinuationsAsynchronously)
            : (options | TaskCreationOptions.RunContinuationsAsynchronously);
    }
}