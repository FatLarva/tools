using System;

namespace Tools.Updates
{
	public interface IUpdateProvider
	{
		event Action VeryEarlyUpdateCalled;
		event Action UpdateCalled;
	}
}
