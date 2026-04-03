using System;
using UnityEngine;

namespace UnityTools
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinMaxRangeAttribute : PropertyAttribute
    {
        public readonly float MinLimit;
        public readonly float MaxLimit;

        public MinMaxRangeAttribute(float minLimit, float maxLimit)
        {
            MinLimit = minLimit;
            MaxLimit = maxLimit;
        }
    }

}