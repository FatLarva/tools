using System;

namespace Tools.Progress
{
	public interface IProgressReporter
	{
		event Action<float> ProgressChanged;

		event Action Completed;

		event Action Cancelled;

		bool IsCompleted { get; }

		bool IsCancelled { get; }

		float CurrentProgress { get; }
	}
}
