using UnityEngine;

namespace UnityTools.GraphicPrimitives
{
    internal readonly struct UvRect
    {
        public readonly Vector2 BottomLeft;
        public readonly Vector2 TopRight;
        private readonly bool _isFulfilled;

        public bool IsEmpty => !_isFulfilled;

        public UvRect(Vector2 bottomLeft, Vector2 topRight)
        {
            BottomLeft = bottomLeft;
            TopRight = topRight;

            _isFulfilled = true;
        }
    }
}