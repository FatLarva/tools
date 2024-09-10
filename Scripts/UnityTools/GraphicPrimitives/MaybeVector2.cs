using System;
using UnityEngine;

namespace UnityTools.GraphicPrimitives
{
    [Serializable]
    internal struct MaybeVector2
    {
        [SerializeField]
        public Vector2 _value;
        [SerializeField]
        public bool HasValue;

        public Vector2 SureValue => _value;
            
        public Vector2? Value
        {
            get => _value;
            set
            {
                HasValue = value.HasValue;
                    
                if (value.HasValue)
                {
                    _value = value.Value;
                }
            }
        }
            
        public static implicit operator Vector2?(MaybeVector2 maybeVector) => maybeVector.HasValue ? maybeVector.Value : null;
        public static implicit operator MaybeVector2(Vector2? nullableVector) => nullableVector.HasValue ? new MaybeVector2 { HasValue = true, Value = nullableVector.Value } : new MaybeVector2 { HasValue = false };
    }
}