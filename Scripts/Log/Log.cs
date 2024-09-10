namespace Logging
{
	public static class Log
	{
		static Log()
		{
			Default = new UnityLog();
			NullLog = null;
		}

		public static ILog Default { get; private set; }
	
		public static ILog NullLog { get; private set; }
	}
}
