using System.Collections.Generic;

namespace Tools
{
	public struct ChangeCheckedValue<T>
	{
		private T _value;

		public T Value => _value;

		public ChangeCheckedValue(T initialValue)
		{
			_value = initialValue;
		}

		public void Set(T newValue)
		{
			_value = newValue;
		}

		public bool TryFlip(T newValue)
		{
			if (!EqualityComparer<T>.Default.Equals(_value, newValue))
			{
				_value = newValue;
				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return _value.ToString();
		}

		public static implicit operator T(ChangeCheckedValue<T> changeCheckedValue) => changeCheckedValue.Value;
		public static implicit operator ChangeCheckedValue<T>(T simpleValue) => new ChangeCheckedValue<T>(simpleValue);
	}
}