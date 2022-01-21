namespace Checkers.Common.Reactive
{
	/// <summary>
	/// Extends <see cref="EventSubscription{TPayload}"/> to invoke the <see cref="EventSubscription{TPayload}.Action"/> delegate in a background thread.
	/// </summary>
	/// <typeparam name="TPayload">The type to use for the generic <see cref="System.Action{TPayload}"/> and <see cref="Predicate{TPayload}"/> types.</typeparam>
	internal class AsyncBackgroundEventSubscription<TPayload> : AsyncEventSubscription<TPayload>
	{
		/// <summary>
		/// Creates a new instance of <see cref="BackgroundEventSubscription{TPayload}"/>.
		/// </summary>
		/// <param name="actionReference">A reference to a delegate of type <see cref="System.Action{TPayload}"/>.</param>
		/// <param name="filterReference">A reference to a delegate of type <see cref="Predicate{TPayload}"/>.</param>
		/// <exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
		/// <exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action{TPayload}"/>,
		/// or the target of <paramref name="filterReference"/> is not of type <see cref="Predicate{TPayload}"/>.</exception>
		public AsyncBackgroundEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference)
			: base(actionReference, filterReference)
		{
		}

		/// <summary>
		/// Invokes the specified <see cref="Task"/> in an asynchronous thread by using a <see cref="Task"/>.
		/// </summary>
		/// <param name="action">The action to execute.</param>
		/// <param name="argument">The arguments for the action to execute.</param>
		public override Task InvokeAction(Func<TPayload, Task> action, TPayload argument)
		{
			return Task.Run(() => action(argument));
		}
	}
	///<summary>
	/// Extends <see cref="EventSubscription{TPayload}"/> to invoke the <see cref="EventSubscription{TPayload}.Action"/> delegate
	/// in a specific <see cref="SynchronizationContext"/>.
	///</summary>
	/// <typeparam name="TPayload">The type to use for the generic <see cref="System.Action{TPayload}"/> and <see cref="Predicate{TPayload}"/> types.</typeparam>
	internal class AsyncDispatcherEventSubscription<TPayload> : AsyncEventSubscription<TPayload>
    {
        private readonly SynchronizationContext syncContext;

        ///<summary>
        /// Creates a new instance of <see cref="BackgroundEventSubscription{TPayload}"/>.
        ///</summary>
        ///<param name="actionReference">A reference to a delegate of type <see cref="System.Action{TPayload}"/>.</param>
        ///<param name="filterReference">A reference to a delegate of type <see cref="Predicate{TPayload}"/>.</param>
        ///<param name="context">The synchronization context to use for UI thread dispatching.</param>
        ///<exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
        ///<exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action{TPayload}"/>,
        ///or the target of <paramref name="filterReference"/> is not of type <see cref="Predicate{TPayload}"/>.</exception>
        public AsyncDispatcherEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference, SynchronizationContext context)
            : base(actionReference, filterReference)
        {
            syncContext = context;
        }

        /// <summary>
        /// Invokes the specified <see cref="Func{TPayload, Task}"/> asynchronously in the specified <see cref="SynchronizationContext"/>.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="argument">The payload to pass <paramref name="action"/> while invoking it.</param>
        public override Task InvokeAction(Func<TPayload, Task> action, TPayload argument)
        {
            var tcs = new TaskCompletionSource<bool>();
            syncContext.Post(async (o) => {
                await action((TPayload)o);
                tcs.SetResult(true);
            }, null);
            return tcs.Task;
        }
    }
}