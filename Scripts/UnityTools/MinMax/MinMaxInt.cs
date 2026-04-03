namespace UnityTools
{
    using System;
    using UnityEngine;

    [Serializable]
    public struct MinMaxInt
    {
        public enum Include
        {
            Min,   // [min..max)
            Max,   // (min..max]
            Both   // [min..max]
        }

        [SerializeField] private int _min;
        [SerializeField] private int _max;

        public int Min => _min;
        public int Max => _max;

        public int Size => _max - _min;
        public float Center => (_min + _max) * 0.5f;

        public MinMaxInt(int min, int max)
        {
            _min = min;
            _max = max;
        }

        // -------- Factory --------
        public static MinMaxInt Of(int min, int max) =>
            new MinMaxInt(min, max);

        public static MinMaxInt FromCenter(int center, int size)
        {
            int half = size / 2;
            return new MinMaxInt(center - half, center + (size - half));
        }

        public static MinMaxInt FromMinSize(int min, int size) =>
            new MinMaxInt(min, min + size);

        public static MinMaxInt FromMaxSize(int max, int size) =>
            new MinMaxInt(max - size, max);

        // -------- Random --------
        /// <summary>
        /// Default = Min (Unity int semantics): [min..max)
        /// Min  => [min..max)
        /// Both => [min..max]
        /// Max  => (min..max]
        /// </summary>
        public int RandomFromRange(Include include = Include.Min)
        {
            return include switch
            {
                Include.Min  => UnityEngine.Random.Range(_min, _max),
                Include.Both => UnityEngine.Random.Range(_min, _max + 1),
                Include.Max  => UnityEngine.Random.Range(_min + 1, _max + 1),
                _ => UnityEngine.Random.Range(_min, _max)
            };
        }

        public override string ToString() => $"[{_min}, {_max}]";
    }

}