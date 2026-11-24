using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tools
{
    [Serializable]
    public struct Optional<T>
    {
        [FormerlySerializedAs("HasValue")]
        [SerializeField] private bool _hasValue;
        [SerializeField] private T _value;

        public bool HasValue => _hasValue;
        public T Value => _hasValue ? _value : throw new InvalidOperationException("The optional value is not set.");

        public Optional(T value)
        {
            _value = value;
            _hasValue = true;
        }

        public bool TryGetValue(out T value)
        {
            value = GetValueOrDefault();
            return _hasValue;
        }

        public T GetValueOrDefault(T fallback = default)
        {
            return _hasValue ? _value : fallback;
        }

        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>(value);
        }
    }
}
