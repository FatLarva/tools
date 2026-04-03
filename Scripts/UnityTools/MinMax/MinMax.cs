namespace UnityTools
{
    using System;
    using UnityEngine;

    [Serializable]
    public struct MinMax
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;

        public float Min => _min;
        public float Max => _max;

        public float Size => _max - _min;
        public float Center => (_min + _max) * 0.5f;

        public MinMax(float min, float max)
        {
            _min = min;
            _max = max;
        }

        // -------- Factory --------
        public static MinMax Of(float min, float max) =>
            new MinMax(min, max);

        public static MinMax FromCenter(float center, float size)
        {
            float half = size * 0.5f;
            return new MinMax(center - half, center + half);
        }

        public static MinMax FromMinSize(float min, float size) =>
            new MinMax(min, min + size);

        public static MinMax FromMaxSize(float max, float size) =>
            new MinMax(max - size, max);

        public float RandomFromRange() =>
            UnityEngine.Random.Range(_min, _max);

        public override string ToString() => $"[{_min}, {_max}]";
    }

}