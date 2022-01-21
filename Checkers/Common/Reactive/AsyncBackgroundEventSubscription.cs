namespace Checkers.Common.Reactive
{
    internal class AsyncBackgroundEventSubscription : AsyncEventSubscription
    {
        /// <summary>
        /// Creates a new instance of <see cref="BackgroundEventSubscription"/>.
        /// </summary>
        /// <param name="actionReference">A reference to a delegate of type <see cref="System.Action"/>.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="actionReference"/> or <see paramref="filterReference"/> are <see langword="null" />.</exception>
        /// <exception cref="ArgumentException">When the target of <paramref name="actionReference"/> is not of type <see cref="System.Action"/>.</exception>
        public AsyncBackgroundEventSubscription(IDelegateReference actionReference)
            : base(actionReference)
        {
        }

        /// <summary>
        /// Invokes the specified <see cref="Task"/> in an asynchronous thread by using a <see cref="Task"/>.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public override Task InvokeAction(Func<Task> action)
        {
            return Task.Run(action);
        }
    }
}