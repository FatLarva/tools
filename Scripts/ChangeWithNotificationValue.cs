using System;
using System.Collections.Generic;

namespace Tools
{
	public class ChangeWithNotificationValue<T>
	{
		private T _value;

		public T Value => _value;

		private readonly List<Action<T>> _subscriptions = new(4);

		public ChangeWithNotificationValue(T initialValue)
		{
			_value = initialValue;
		}

		public void Set(T newValue)
		{
			var oldValue = _value;
			_value = newValue;

			if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
			{
				NotifySubscribers();
			}
		}

		public bool TryFlip(T newValue)
		{
			if (!EqualityComparer<T>.Default.Equals(_value, newValue))
			{
				_value = newValue;
				NotifySubscribers();

				return true;
			}

			return false;
		}

		public void SubscribeOnChange(Action<T> callback)
		{
			_subscriptions.Add(callback);
		}

		public void UnsubscribeOnChange(Action<T> callback)
		{
			_subscriptions.Remove(callback);
		}

		public override string ToString()
		{
			return _value.ToString();
		}

		private void NotifySubscribers()
		{
			foreach (var action in _subscriptions)
			{
				action.Invoke(_value);
			}
		}

		public static implicit operator T(ChangeWithNotificationValue<T> changeCheckedValue) => changeCheckedValue.Value;
		public static implicit operator ChangeWithNotificationValue<T>(T simpleValue) => new ChangeWithNotificationValue<T>(simpleValue);
	}
}