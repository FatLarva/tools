namespace Tools.Progress
{
	public interface IProgressConsumer
	{
		void SetProgress(float progress);

		void Complete(bool isSuccess);

		void Cancel();
	}
}
