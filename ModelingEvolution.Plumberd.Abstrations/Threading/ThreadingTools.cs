using ModelingEvolution.Plumberd.RelationDataModeling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Threading
{

    public static class ThreadingTools
    {
        internal interface ICancellationNotification
        {
            void OnCanceled();
        }

        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentException(nameof(task));

            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return WithCancellationSlow(task, continueOnCapturedContext: false, cancellationToken: cancellationToken);
        }
        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <param name="continueOnCapturedContext">A value indicating whether *internal* continuations required to respond to cancellation should run on the current <see cref="SynchronizationContext"/>.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        internal static Task WithCancellation(this Task task, bool continueOnCapturedContext, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentException(nameof(task));

            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return WithCancellationSlow(task, continueOnCapturedContext, cancellationToken);
        }

       
        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <typeparam name="T">The type of value returned by the task.</typeparam>
        /// <param name="task">The task to wrap.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        private static async Task<T> WithCancellationSlow<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentException(nameof(task));
            if(!cancellationToken.CanBeCanceled) throw new ArgumentException(nameof(cancellationToken));

            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Rethrow any fault/cancellation exception, even if we awaited above.
            // But if we skipped the above if branch, this will actually yield
            // on an incompleted task.
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Wraps a task with one that will complete as cancelled based on a cancellation token,
        /// allowing someone to await a task but be able to break out early by cancelling the token.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <param name="continueOnCapturedContext">A value indicating whether *internal* continuations required to respond to cancellation should run on the current <see cref="SynchronizationContext"/>.</param>
        /// <param name="cancellationToken">The token that can be canceled to break out of the await.</param>
        /// <returns>The wrapping task.</returns>
        private static async Task WithCancellationSlow(this Task task, bool continueOnCapturedContext, CancellationToken cancellationToken)
        {
            if (task == null) throw new ArgumentException(nameof(task));
            if (!cancellationToken.CanBeCanceled) throw new ArgumentException(nameof(cancellationToken));


            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(continueOnCapturedContext))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // Rethrow any fault/cancellation exception, even if we awaited above.
            // But if we skipped the above if branch, this will actually yield
            // on an incompleted task.
            await task.ConfigureAwait(continueOnCapturedContext);
        }

        /// <summary>
        /// A state object for tracking cancellation and a TaskCompletionSource.
        /// </summary>
        /// <typeparam name="T">The type of value returned from a task.</typeparam>
        /// <remarks>
        /// We use this class so that we only allocate one object to support all continuations
        /// required for cancellation handling, rather than a special closure and delegate for each one.
        /// </remarks>
        private class CancelableTaskCompletionSource<T>
        {
            /// <summary>
            /// The ID of the thread on which this instance was created.
            /// </summary>
            private readonly int ownerThreadId = Environment.CurrentManagedThreadId;

            /// <summary>
            /// Initializes a new instance of the <see cref="CancelableTaskCompletionSource{T}"/> class.
            /// </summary>
            /// <param name="taskCompletionSource">The task completion source.</param>
            /// <param name="cancellationCallback">A callback to invoke when cancellation occurs.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            internal CancelableTaskCompletionSource(TaskCompletionSource<T> taskCompletionSource, ICancellationNotification? cancellationCallback, CancellationToken cancellationToken)
            {
                this.TaskCompletionSource = taskCompletionSource ?? throw new ArgumentNullException(nameof(taskCompletionSource));
                this.CancellationToken = cancellationToken;
                this.CancellationCallback = cancellationCallback;
            }

            /// <summary>
            /// Gets the cancellation token.
            /// </summary>
            internal CancellationToken CancellationToken { get; }

            /// <summary>
            /// Gets the Task completion source.
            /// </summary>
            internal TaskCompletionSource<T> TaskCompletionSource { get; }

            internal ICancellationNotification? CancellationCallback { get; }

            /// <summary>
            /// Gets or sets the cancellation token registration.
            /// </summary>
            internal CancellationTokenRegistration CancellationTokenRegistration { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the continuation has been scheduled (and not run inline).
            /// </summary>
            internal bool ContinuationScheduled { get; set; }

            /// <summary>
            /// Gets a value indicating whether the caller is on the same thread as the one that created this instance.
            /// </summary>
            internal bool OnOwnerThread => Environment.CurrentManagedThreadId == this.ownerThreadId;
        }
    }
}
