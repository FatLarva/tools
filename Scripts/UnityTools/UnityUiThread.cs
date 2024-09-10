using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UnityTools
{
	public class UnityUiThread
	{
		public static readonly UnityUiThread Thread;
		
		static UnityUiThread()
		{
			Thread = new UnityUiThread(SynchronizationContext.Current);
		}
		
		private readonly SynchronizationContext _synchronizationContext;

		private UnityUiThread(SynchronizationContext synchronizationContext)
		{
			_synchronizationContext = synchronizationContext;
		}
		
		public SynchronizationContextAwaiter GetAwaiter()
		{
			return new SynchronizationContextAwaiter(_synchronizationContext);
		}
	}
	
	public readonly struct SynchronizationContextAwaiter : ICriticalNotifyCompletion
	{
		private static readonly SendOrPostCallback PostCallback = continuation => ((Action)continuation).Invoke();

		private readonly SynchronizationContext _context;
		public SynchronizationContextAwaiter(SynchronizationContext context)
		{
			_context = context;
		}

		public bool IsCompleted => _context == SynchronizationContext.Current;

		public void OnCompleted(Action continuation) => _context.Post(PostCallback, continuation);
		public void UnsafeOnCompleted(Action continuation)=> _context.Post(PostCallback, continuation);

		public void GetResult() { }
	}
}
