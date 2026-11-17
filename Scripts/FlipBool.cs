namespace Tools
{
	public struct FlipBool
	{
		private bool _internalState;

		public bool Value => _internalState;

		public FlipBool(bool initialState)
		{
			_internalState = initialState;
		}

		public void Set(bool newValue)
		{
			_internalState = newValue;
		}

		public bool TrySet(bool newValue)
		{
			if (_internalState != newValue)
			{
				_internalState = newValue;
				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return _internalState.ToString();
		}

		public static implicit operator bool(FlipBool flipBool)
		{
			return flipBool.Value;
		}

		public static implicit operator FlipBool(bool simpleBool)
		{
			return new FlipBool(simpleBool);
		}
	}
}
