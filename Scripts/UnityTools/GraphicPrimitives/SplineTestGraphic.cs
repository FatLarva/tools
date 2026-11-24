using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Closed Spline Graphic", 12)]
    public class SplineTestGraphic : PrimitiveGraphic
    {
        internal const int MinimumKnots = 3;
        internal const int MaximumKnots = 64;

        [Serializable]
        public struct Knot
        {
            public Vector2 Position;
            public Vector2 Incoming;
            public Vector2 Outgoing;
            public bool AutoIncoming;
            public bool AutoOutgoing;
            [SerializeField] internal BezierTangentMode Mode;

            public Knot(Vector2 position, Vector2 incoming, Vector2 outgoing)
            {
                Position = position;
                Incoming = incoming;
                Outgoing = outgoing;
                AutoIncoming = false;
                AutoOutgoing = false;
                Mode = BezierTangentMode.Broken;
            }
        }

        [SerializeField] private ClosedShapeMode _mode = ClosedShapeMode.Fill;
        [SerializeField, Range(2, 48)] private int _samplesPerSegment = 12;
        [SerializeField] private Knot[] _knots =
        {
            new Knot(new Vector2(0.12f, 0.55f), new Vector2(-0.01f, -0.22f), new Vector2(0.02f, 0.24f)),
            new Knot(new Vector2(0.48f, 0.89f), new Vector2(-0.25f, -0.01f), new Vector2(0.18f, 0.06f)),
            new Knot(new Vector2(0.89f, 0.57f), new Vector2(0.05f, 0.19f), new Vector2(0.01f, -0.26f)),
            new Knot(new Vector2(0.51f, 0.20f), new Vector2(0.23f, -0.11f), new Vector2(-0.27f, -0.11f)),
        };

        [SerializeField] private bool _shouldUseGradient;
        [SerializeField] private ContourGradient _gradientType;
        [SerializeField] private Gradient _gradientColor = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _edgeThickness = 1f;
        [SerializeField] private Color _edgeColor = Color.white;
        [SerializeField] private bool _shouldUseEdgeGradient;
        [SerializeField] private ContourGradient _edgeGradientType;
        [SerializeField] private Gradient _edgeGradient = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _antiAliasingLayerWidth = 1f;
        [SerializeField, Min(0)] private float _innerAntiAliasingLayerWidth;

        internal bool InvalidContour { get; private set; }

        public ClosedShapeMode Mode
        {
            get { return _mode; }
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    SetVerticesDirty();
                }
            }
        }

        public void SetKnots(ReadOnlySpan<Knot> knots)
        {
            if (knots.Length < MinimumKnots || knots.Length > MaximumKnots)
            {
                throw new ArgumentOutOfRangeException(nameof(knots), "A closed spline needs between 3 and 64 knots.");
            }

            _knots = knots.ToArray();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            InvalidContour = false;
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            if (_knots == null || _knots.Length < MinimumKnots || _knots.Length > MaximumKnots)
            {
                InvalidContour = true;
                return;
            }

            int detail = GetSegmentDetail(_samplesPerSegment, _knots.Length);
            Span<Vector2> contour = stackalloc Vector2[_knots.Length * detail];
            int position = 0;
            for (int i = 0; i < _knots.Length; i++)
            {
                Knot first = _knots[i];
                Knot last = _knots[(i + 1) % _knots.Length];
                CubicBezierSegment curve = GetCurve(in first, in last);
                for (int step = 0; step < detail; step++)
                {
                    Vector2 point = curve.Evaluate((float)step / detail);
                    contour[position++] = rect.min + Vector2.Scale(rect.size, point);
                }
            }

            var fill = new ContourPaint(color, _shouldUseGradient ? _gradientColor : null, _gradientType);
            var edge = new ContourPaint(_edgeColor, _shouldUseEdgeGradient ? _edgeGradient : null, _edgeGradientType);
            InvalidContour = !ClosedContour.Draw(mesh, contour, rect, GraphicMesh.GetSpriteUv(null), _mode, _edgeThickness,
                _antiAliasingLayerWidth, _innerAntiAliasingLayerWidth, fill, edge);
        }

        internal static int GetSegmentDetail(int requested, int knotCount)
        {
            return Mathf.Clamp(requested, 2, Mathf.Min(48, ClosedContour.MaximumSamples / knotCount));
        }

        internal static CubicBezierSegment GetCurve(in Knot first, in Knot last)
        {
            var outgoing = first.AutoOutgoing
                ? Vector2.LerpUnclamped(first.Position, last.Position, 1f / 3f)
                : first.Position + first.Outgoing;
            var incoming = last.AutoIncoming
                ? Vector2.LerpUnclamped(first.Position, last.Position, 2f / 3f)
                : last.Position + last.Incoming;
            return new CubicBezierSegment(first.Position, outgoing, incoming, last.Position);
        }
    }
}
