namespace Checkers.Common.Reactive
{
	internal class AsyncEventSubscription : IEventSubscription
	{
		private readonly IDelegateReference _actionReference;

		///<summary>
		/// Creates a new instance of <see cref="EventSubscription"/>.
		///</summary>
		///<param name="actionReference">A reference to a delegate of type <see cref="System.Action"/>.</param>
		///<exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
		///<exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action"/>.</exception>
		public AsyncEventSubscription(IDelegateReference actionReference)
		{
			if (actionReference == null)
				throw new ArgumentNullException(nameof(actionReference));
			if (!(actionReference.Target is Action))
				throw new ArgumentException("InvalidDelegateRerefenceTypeException");

			_actionReference = actionReference;
		}

		/// <summary>
		/// Gets the target <see cref="System.Threading.Tasks"/> that is referenced by the <see cref="IDelegateReference"/>.
		/// </summary>
		/// <value>An <see cref="System.Action"/> or <see langword="null" /> if the referenced target is not alive.</value>
		public Func<Task> Action
		{
			get { return (Func<Task>)_actionReference.Target; }
		}

		public Delegate Delegate
		{
			get
			{
				return _actionReference.Target;
			}
		}

		/// <summary>
		/// Gets or sets a <see cref="SubscriptionToken"/> that identifies this <see cref="IEventSubscription"/>.
		/// </summary>
		/// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
		public SubscriptionToken SubscriptionToken { get; set; }

		/// <summary>
		/// Gets the execution strategy to publish this event.
		/// </summary>
		/// <returns>An <see cref="System.Action"/> with the execution strategy, or <see langword="null" /> if the <see cref="IEventSubscription"/> is no longer valid.</returns>
		/// <remarks>
		/// If <see cref="Action"/>is no longer valid because it was
		/// garbage collected, this method will return <see langword="null" />.
		/// Otherwise it will return a delegate that evaluates the <see cref="Filter"/> and if it
		/// returns <see langword="true" /> will then call <see cref="InvokeAction"/>. The returned
		/// delegate holds a hard reference to the <see cref="Action"/> target
		/// <see cref="Delegate">delegates</see>. As long as the returned delegate is not garbage collected,
		/// the <see cref="Action"/> references delegates won't get collected either.
		/// </remarks>
		public virtual Func<object[], Task> GetExecutionStrategy()
		{
			Func<Task> action = this.Action;
			if (action != null)
			{
				return (arg) => { return InvokeAction(action); };
			}
			return null;
		}

		/// <summary>
		/// Invokes the specified <see cref="System.Action{TPayload}"/> synchronously when not overridden.
		/// </summary>
		/// <param name="action">The action to execute.</param>
		/// <exception cref="ArgumentNullException">An <see cref="ArgumentNullException"/> is thrown if <paramref name="action"/> is null.</exception>
		public virtual Task InvokeAction(Func<Task> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			return action();
		}
	}
	internal class AsyncDispatcherEventSubscription : AsyncEventSubscription
	{
		private readonly SynchronizationContext syncContext;

		///<summary>
		/// Creates a new instance of <see cref="BackgroundEventSubscription"/>.
		///</summary>
		///<param name="actionReference">A reference to a delegate of type <see cref="System.Action{TPayload}"/>.</param>
		///<param name="context">The synchronization context to use for UI thread dispatching.</param>
		///<exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
		///<exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action{TPayload}"/>.</exception>
		public AsyncDispatcherEventSubscription(IDelegateReference actionReference, SynchronizationContext context)
			: base(actionReference)
		{
			syncContext = context;
		}

		/// <summary>
		/// Invokes the specified <see cref="Func{Task}"/> asynchronously in the specified <see cref="SynchronizationContext"/>.
		/// </summary>
		/// <param name="action">The action to execute.</param>
		public override Task InvokeAction(Func<Task> action)
		{
			var tcs = new TaskCompletionSource<bool>();
			syncContext.Post(async (o) => {
				await action();
				tcs.SetResult(true);
			}, null);
			return tcs.Task;
		}
	}

    /// <summary>
	/// Provides a way to retrieve a <see cref="Delegate"/> to execute an action depending
	/// on the value of a second filter predicate that returns true if the action should execute.
	/// </summary>
	/// <typeparam name="TPayload">The type to use for the generic <see cref="System.Action{TPayload}"/> and <see cref="Predicate{TPayload}"/> types.</typeparam>
	internal class AsyncEventSubscription<TPayload> : IEventSubscription
	{
		private readonly IDelegateReference _actionReference;
		private readonly IDelegateReference _filterReference;

		///<summary>
		/// Creates a new instance of <see cref="EventSubscription{TPayload}"/>.
		///</summary>
		///<param name="actionReference">A reference to a delegate of type <see cref="System.Action{TPayload}"/>.</param>
		///<param name="filterReference">A reference to a delegate of type <see cref="Predicate{TPayload}"/>.</param>
		///<exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
		///<exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action{TPayload}"/>,
		///or the target of <paramref name="filterReference"/> is not of type <see cref="Predicate{TPayload}"/>.</exception>
		public AsyncEventSubscription(IDelegateReference actionReference, IDelegateReference filterReference)
		{
			if (actionReference == null)
			{
				throw new ArgumentNullException(nameof(actionReference));
			}
			if (!(actionReference.Target is Func<TPayload, Task>))
			{
				throw new ArgumentException("Invalid target delegate.");
			}

			if (filterReference == null)
			{
				throw new ArgumentNullException(nameof(filterReference));
			}
			if (!(filterReference.Target is Predicate<TPayload>))
			{
                throw new ArgumentException("Invalid target delegate.");
			}

			_actionReference = actionReference;
			_filterReference = filterReference;
		}

		/// <summary>
		/// Gets the target <see cref="System.Action{T}"/> that is referenced by the <see cref="IDelegateReference"/>.
		/// </summary>
		/// <value>An <see cref="System.Action{T}"/> or <see langword="null" /> if the referenced target is not alive.</value>
		public Func<TPayload, Task> Action
		{
			get { return (Func<TPayload, Task>)_actionReference.Target; }
		}

		/// <summary>
		/// Gets the delegate of the action.
		/// </summary>
		/// <value>The delegate.</value>
		public Delegate Delegate
		{
			get
			{
				return _actionReference.Target;
			}
		}

		/// <summary>
		/// Gets the target <see cref="Predicate{T}"/> that is referenced by the <see cref="IDelegateReference"/>.
		/// </summary>
		/// <value>An <see cref="Predicate{T}"/> or <see langword="null" /> if the referenced target is not alive.</value>
		public Predicate<TPayload> Filter
		{
			get { return (Predicate<TPayload>)_filterReference.Target; }
		}

		/// <summary>
		/// Gets or sets a <see cref="SubscriptionToken"/> that identifies this <see cref="IEventSubscription"/>.
		/// </summary>
		/// <value>A token that identifies this <see cref="IEventSubscription"/>.</value>
		public SubscriptionToken SubscriptionToken { get; set; }

		/// <summary>
		/// Gets the execution strategy to publish this event.
		/// </summary>
		/// <returns>An <see cref="System.Action{T}"/> with the execution strategy, or <see langword="null" /> if the <see cref="IEventSubscription"/> is no longer valid.</returns>
		/// <remarks>
		/// If <see cref="Action"/> or <see cref="Filter"/> are no longer valid because they were
		/// garbage collected, this method will return <see langword="null" />.
		/// Otherwise it will return a delegate that evaluates the <see cref="Filter"/> and if it
		/// returns <see langword="true" /> will then call <see cref="InvokeAction"/>. The returned
		/// delegate holds hard references to the <see cref="Action"/> and <see cref="Filter"/> target
		/// <see cref="Delegate">delegates</see>. As long as the returned delegate is not garbage collected,
		/// the <see cref="Action"/> and <see cref="Filter"/> references delegates won't get collected either.
		/// </remarks>
		public virtual Func<object[], Task> GetExecutionStrategy()
		{
			Func<TPayload, Task> action = this.Action;
			Predicate<TPayload> filter = this.Filter;
			if (action != null && filter != null)
			{
				return arguments => {
					TPayload argument = default(TPayload);
					if (arguments != null && arguments.Length > 0 && arguments[0] != null)
					{
						argument = (TPayload)arguments[0];
					}
					if (filter(argument))
					{
						return InvokeAction(action, argument);
					}
					return AsyncHelpers.Return();
				};
			}
			return null;
		}

		/// <summary>
		/// Invokes the specified <see cref="System.Action{TPayload}"/> synchronously when not overridden.
		/// </summary>
		/// <param name="action">The action to execute.</param>
		/// <param name="argument">The payload to pass <paramref name="action"/> while invoking it.</param>
		/// <exception cref="ArgumentNullException">An <see cref="ArgumentNullException"/> is thrown if <paramref name="action"/> is null.</exception>
		public virtual Task InvokeAction(Func<TPayload, Task> action, TPayload argument)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			return action(argument);
		}
	}
}
