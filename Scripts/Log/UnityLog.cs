using System;
using UnityEngine;

namespace Logging
{
	public class UnityLog : ILog
	{
		public void Info(string message)
		{
			Debug.Log(message);
		}

		public void Warning(string message)
		{
			Debug.LogWarning(message);
		}

		public void Error(string message)
		{
			Debug.LogError(message);
		}

		public void Exception(string message, Exception exception)
		{
			Debug.LogError(message);
			Debug.LogException(exception);
		}
	}
}
