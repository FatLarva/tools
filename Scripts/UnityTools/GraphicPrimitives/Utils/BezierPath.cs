using UnityEngine;

namespace UnityTools.GraphicPrimitives
{
    internal enum BezierTangentMode
    {
        Broken = 0,
        Mirrored = 1,
        Continuous = 2,
    }

    internal readonly struct CubicBezierSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 Outgoing;
        public readonly Vector2 Incoming;
        public readonly Vector2 End;

        public CubicBezierSegment(Vector2 start, Vector2 outgoing, Vector2 incoming, Vector2 end)
        {
            Start = start;
            Outgoing = outgoing;
            Incoming = incoming;
            End = end;
        }

        public Vector2 Evaluate(float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * inverse * Start + 3f * inverse * inverse * t * Outgoing +
                3f * inverse * t * t * Incoming + t * t * t * End;
        }

        public void Split(float t, out CubicBezierSegment first, out CubicBezierSegment second)
        {
            var outgoing = Vector2.LerpUnclamped(Start, Outgoing, t);
            var inner = Vector2.LerpUnclamped(Outgoing, Incoming, t);
            var incoming = Vector2.LerpUnclamped(Incoming, End, t);
            var left = Vector2.LerpUnclamped(outgoing, inner, t);
            var right = Vector2.LerpUnclamped(inner, incoming, t);
            var midpoint = Vector2.LerpUnclamped(left, right, t);
            first = new CubicBezierSegment(Start, outgoing, left, midpoint);
            second = new CubicBezierSegment(midpoint, right, incoming, End);
        }
    }

    internal static class BezierPath
    {
        private const float MinimumDirectionLength = 0.0001f;

        internal static Vector2 GetOppositeTangent(BezierTangentMode mode, Vector2 point, Vector2 moved, Vector2 opposite)
        {
            var offset = moved - point;
            if (mode == BezierTangentMode.Mirrored)
            {
                return point - offset;
            }

            if (mode == BezierTangentMode.Continuous && offset.sqrMagnitude > MinimumDirectionLength * MinimumDirectionLength)
            {
                return point - offset.normalized * Vector2.Distance(point, opposite);
            }

            return opposite;
        }
    }
}
