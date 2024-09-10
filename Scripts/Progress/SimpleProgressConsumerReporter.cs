using System;

namespace Tools.Progress
{
	public class SimpleProgressConsumerReporter : IProgressConsumer, IProgressReporter
	{
		public event Action<float> ProgressChanged;
		public event Action Completed;
		public event Action Cancelled;

		public bool IsCompleted { get; private set; }

		public bool IsCancelled { get; private set; }

		public float CurrentProgress{ get; private set; }

		public void Reset()
		{
			IsCompleted = false;
			IsCancelled = false;
			CurrentProgress = 0.0f;
		}

		public void Cancel()
		{
			IsCancelled = true;
			Cancelled?.Invoke();
		}

		public void Complete(bool isSuccess)
		{
			IsCompleted = true;
			Completed?.Invoke();
		}

		public void SetProgress(float progress)
		{
			CurrentProgress = progress;
			ProgressChanged?.Invoke(progress);
		}
	}
}
