using System.Threading;

namespace Tools
{
	public class AtomicBool
	{
		private int _internalState;

		public bool Value => Interlocked.CompareExchange(ref _internalState, 1, 1) == 1;

		public AtomicBool(bool initialState)
		{
			_internalState = initialState ? 1 : 0;
		}

		public void Set(bool newValue)
		{
			Interlocked.Exchange(ref _internalState, newValue ? 1 : 0);
		}

		public bool TryFlip(bool newValue)
		{
			var newIntValue = newValue ? 1 : 0;
			var expectedIntValue = 1 - newIntValue;

			return Interlocked.CompareExchange(ref _internalState, newIntValue, expectedIntValue) == expectedIntValue;
		}

		public override string ToString()
		{
			return _internalState.ToString();
		}

		public static implicit operator bool(AtomicBool atomicBool)
		{
			return atomicBool.Value;
		}
	}
}
