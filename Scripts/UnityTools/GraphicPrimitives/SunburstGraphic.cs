using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [AddComponentMenu("UI/Sunburst Graphic", 12)]
    public class SunburstGraphic : PrimitiveImage
    {
        public enum ProfileType
        {
            Sharp = 1,
            Smooth = 2,
        }

        private enum Mode
        {
            [InspectorName("Fill")]
            FillInside = 0,
            Edge = 2,
            EdgeAndFill = 3,
        }

        private enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        }

        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }

        private struct Vert
        {
            public Vector2 Position;
            public Vector2 Outward;
            public Vector2 SmoothingOutward;
            public float Distance;
        }

        private struct StrokeVert
        {
            public int InnerAa;
            public int Inner;
            public int Outer;
            public int OuterAa;
        }

        private readonly struct Paint
        {
            private readonly Rect _rect;
            private readonly Vector4 _uv;
            private readonly Gradient _gradient;
            private readonly GradientType _axis;
            private readonly Color _color;

            public Paint(Rect rect, Vector4 uv, Color color, Gradient gradient, GradientType axis)
            {
                _rect = rect;
                _uv = uv;
                _color = color;
                _gradient = gradient;
                _axis = axis;
            }

            public int Add(VertexHelper mesh, Vector2 position, float opacity = 1f)
            {
                Color tint = _color;
                if (_gradient != null)
                {
                    float x = (position.x - _rect.xMin) / _rect.width;
                    float y = (position.y - _rect.yMin) / _rect.height;
                    float t = _axis switch
                    {
                        GradientType.Horizontal => x,
                        GradientType.Vertical => y,
                        _ => new Vector2(x * 2f - 1f, y * 2f - 1f).magnitude,
                    };
                    tint = _gradient.Evaluate(Mathf.Clamp01(t));
                }

                tint.a *= opacity;
                int index = mesh.currentVertCount;
                mesh.AddVert(position, tint, GraphicMesh.MapUv(position, _rect, _uv));
                return index;
            }
        }

        [SerializeField] private ProfileType _profile = ProfileType.Sharp;
        [SerializeField, Range(0, 360)] private int _startAngle;
        [SerializeField, Min(3)] private int _peaksCount = 10;
        [SerializeField, Range(1f, 2f)] private float _outerDisplacementFactor = 1f;
        [SerializeField, Range(0f, 1f)] private float _innerDisplacementFactor = 1f;
        [SerializeField] private bool _keepCircle;
        [SerializeField] private Mode _mode;
        [SerializeField] private EdgeThicknessMode _edgeThicknessMode;
        [SerializeField, Min(0)] private float _edgeThickness = 1f;
        [SerializeField, Range(0, 1)] private float _edgeThicknessRelative;
        [SerializeField] private bool _isDashed;
        [SerializeField, Min(0)] private float _dashLength = 10f;
        [SerializeField, Min(0)] private float _gapLength = 10f;
        [SerializeField, Range(0, 1)] private float _dashPhase;
        [SerializeField, Min(0)] private float _antiAliasingLayerWidth;
        [SerializeField, Min(0)] private float _innerAntiAliasingLayerWidth;
        [SerializeField] private bool _shouldUseGradient;
        [SerializeField] private GradientType _gradientType;
        [SerializeField] private Gradient _gradientColor = ClosedContour.WhiteGradient();
        [SerializeField] private Color _edgeColor = Color.white;
        [SerializeField] private bool _shouldUseGradientForEdge;
        [SerializeField] private Gradient _edgeColorGradient = ClosedContour.WhiteGradient();
        [SerializeField] private GradientType _edgeGradientType;
        [SerializeField, Range(-1, 1)] private float _innerTangentPointShift;
        [SerializeField, Range(-1, 1)] private float _outerTangentPointShift;
        [SerializeField, Range(-1, 1)] private float _innerTangentPointShiftPerp;
        [SerializeField, Range(-1, 1)] private float _outerTangentPointShiftPerp;
        [SerializeField, Range(2, 80)] private int _curvesSmoothness = 12;

        internal bool MeshBudgetExceeded { get; private set; }
        internal int VertexCount { get; private set; }
        internal int TriangleCount { get; private set; }

        public ProfileType Profile
        {
            get { return _profile; }
            set
            {
                if (_profile != value)
                {
                    _profile = value;
                    SetVerticesDirty();
                }
            }
        }

        public Color EdgeColor
        {
            get { return _edgeColor; }
            set
            {
                if (_edgeColor != value)
                {
                    _edgeColor = value;
                    SetVerticesDirty();
                }
            }
        }

        public Color MainColor
        {
            get { return color; }
            set { color = value; }
        }

        public float EdgeThicknessAbsolute
        {
            get { return _edgeThickness; }
            set
            {
                float thickness = FiniteWidth(value);
                if (_edgeThickness != thickness)
                {
                    _edgeThickness = thickness;
                    SetVerticesDirty();
                }
            }
        }

        public float EdgeThicknessRelative
        {
            get { return _edgeThicknessRelative; }
            set
            {
                float thickness = Mathf.Clamp01(FiniteWidth(value));
                if (_edgeThicknessRelative != thickness)
                {
                    _edgeThicknessRelative = thickness;
                    SetVerticesDirty();
                }
            }
        }

        public void SetColors(Color mainColor, Color edgeColor)
        {
            MainColor = mainColor;
            EdgeColor = edgeColor;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            MeshBudgetExceeded = false;
            GenerateMesh(mesh);
            VertexCount = mesh.currentVertCount;
            TriangleCount = mesh.currentIndexCount / 3;
        }

        private void GenerateMesh(VertexHelper mesh)
        {
            Rect rect = GetPixelAdjustedRect();
            float halfSide = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (!(halfSide > 0f))
            {
                return;
            }

            if (_keepCircle)
            {
                rect = new Rect(rect.center - Vector2.one * halfSide, Vector2.one * (halfSide * 2f));
            }

            int peaks = Mathf.Max(3, _peaksCount);
            bool smooth = Profile == ProfileType.Smooth;
            if (peaks > (GraphicMesh.MaximumVertices - 5) / 16)
            {
                MeshBudgetExceeded = true;
                return;
            }

            int corners = peaks * 2;
            int detail = smooth ? Mathf.Clamp(_curvesSmoothness, 2, 80) : 1;
            detail = Mathf.Min(detail, (GraphicMesh.MaximumVertices - 5) / (corners * 8));
            int pointCount = corners * detail;
            using var owner = SpanBufferSharedPool.RentBuffer(pointCount + 1, out SpanBuffer<Vert> pointsBuffer);
            BuildOutline(rect, corners, detail, ref pointsBuffer);
            Span<Vert> points = pointsBuffer.FilledBufferSlice();
            if (points[^1].Distance <= 0f)
            {
                return;
            }

            float edge = _edgeThicknessMode == EdgeThicknessMode.Relative
                ? _edgeThicknessRelative * halfSide
                : _edgeThickness;
            float aa = PrepareOuterSmoothing(points, rect.center, FiniteWidth(_antiAliasingLayerWidth));
            Vector4 uv = GraphicMesh.GetSpriteUv(overrideSprite);
            var fill = new Paint(rect, uv, color, _shouldUseGradient ? _gradientColor : null, _gradientType);
            var stroke = new Paint(rect, uv, _edgeColor, _shouldUseGradientForEdge ? _edgeColorGradient : null, _edgeGradientType);
            bool fillEnabled = _mode != Mode.Edge;
            edge = _mode == Mode.FillInside ? 0f : LimitBandDepth(points, rect.center, 0f, Mathf.Clamp(FiniteWidth(edge), 0f, halfSide));
            bool strokeEnabled = _mode != Mode.FillInside && edge > 0f;
            float requestedInner = Mathf.Clamp(edge + FiniteWidth(_innerAntiAliasingLayerWidth), edge, halfSide);
            float innerAa = strokeEnabled ? LimitBandDepth(points, rect.center, edge, requestedInner) - edge : 0f;
            float dashLength = FiniteWidth(_dashLength);
            float gapLength = FiniteWidth(_gapLength);
            bool dashed = strokeEnabled && _isDashed && gapLength > 0f;
            if (dashed && dashLength > 0f)
            {
                float dashCount = points[^1].Distance / (dashLength + gapLength);
                if (dashCount > (GraphicMesh.MaximumVertices - pointCount * 8 - 5) / 16f)
                {
                    MeshBudgetExceeded = true;
                    if (fillEnabled)
                    {
                        DrawFill(mesh, points, rect.center, aa, fill);
                    }
                    return;
                }
            }

            if (fillEnabled && (!strokeEnabled || dashed))
            {
                DrawFill(mesh, points, rect.center, aa, fill);
            }

            if (!strokeEnabled || (dashed && dashLength <= 0f))
            {
                return;
            }

            if (dashed)
            {
                DrawDashes(mesh, points, rect.center, edge, innerAa, aa, stroke, dashLength, gapLength);
            }
            else
            {
                DrawSolidStroke(mesh, points, rect.center, edge, innerAa, aa, fillEnabled, fill, stroke);
            }
        }

        private void BuildOutline(Rect rect, int corners, int detail, ref SpanBuffer<Vert> buffer)
        {
            Vector2 center = rect.center;
            Vector2 halfSize = rect.size * 0.5f;
            float inner = Mathf.Clamp(FiniteWidth(_innerDisplacementFactor), detail > 1 ? 0.005f : 0f, 1f);
            float outer = Mathf.Clamp(FiniteWidth(_outerDisplacementFactor), 1f, 2f);
            float origin = Mathf.Repeat(_startAngle, 360f) * Mathf.Deg2Rad;
            float step = 2f * Mathf.PI / corners;
            Vector2 previous = default;
            float distance = 0f;
            for (int i = 0; i < corners; i++)
            {
                float angle = origin + step * i;
                Vector2 firstDirection = Direction(angle);
                Vector2 lastDirection = Direction(angle + step);
                bool isValley = (i & 1) == 0;
                float firstRadius = isValley ? inner : outer;
                float lastRadius = isValley ? outer : inner;
                Vector2 first = firstDirection * firstRadius;
                Vector2 last = lastDirection * lastRadius;
                Vector2 firstControl = default;
                Vector2 lastControl = default;
                if (detail > 1)
                {
                    firstControl = GetControl(angle, step, firstRadius, inner, outer, isValley, 1f);
                    lastControl = GetControl(angle + step, step, lastRadius, inner, outer, !isValley, -1f);
                }

                for (int j = 0; j < detail; j++)
                {
                    float t = (float)j / detail;
                    Vector2 sample = j == 0 ? first : Bezier(first, firstControl, lastControl, last, t);
                    Vector2 position = Vector2.Scale(halfSize, sample);
                    if (i != 0 || j != 0)
                    {
                        distance += Vector2.Distance(position, previous);
                    }

                    buffer.Add(new Vert { Position = center + position, Distance = distance });
                    previous = position;
                }
            }

            Span<Vert> rows = buffer.FilledBufferSlice();
            for (int i = 0; i < rows.Length; i++)
            {
                Vector2 preceding = rows[(i + rows.Length - 1) % rows.Length].Position;
                Vector2 following = rows[(i + 1) % rows.Length].Position;
                var outward = GetOutward(preceding, rows[i].Position, following);
                rows[i].Outward = outward;
                rows[i].SmoothingOutward = outward;
            }

            Vert end = rows[0];
            end.Distance = distance + Vector2.Distance(rows[^1].Position, end.Position);
            buffer.Add(end);
        }

        private Vector2 GetControl(float angle, float step, float radius, float inner, float outer, bool valley, float side)
        {
            float radialShift = FiniteSignedUnit(valley ? _innerTangentPointShift : _outerTangentPointShift);
            float sideShift = FiniteSignedUnit(valley ? _innerTangentPointShiftPerp : _outerTangentPointShiftPerp);
            float neutralRadius = radius * Mathf.Cos(step * 0.5f);
            float difference = outer - inner;
            float minimumRadius = Mathf.Max(inner * 0.5f, neutralRadius - difference);
            float maximumRadius = Mathf.Min(outer + difference * 0.35f, neutralRadius + difference);
            float controlRadius = radialShift < 0f
                ? Mathf.Lerp(neutralRadius, minimumRadius, -radialShift)
                : Mathf.Lerp(neutralRadius, maximumRadius, radialShift);

            // The first and last controls stay ordered in separate halves of the ray in ellipse space.
            const float neutralFraction = 0.33f;
            float angleFraction = sideShift < 0f
                ? Mathf.Lerp(neutralFraction, 0.48f, -sideShift)
                : Mathf.Lerp(neutralFraction, 0.08f, sideShift);
            return Direction(angle + step * angleFraction * side) * controlRadius;
        }

        private static Vector2 Direction(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Vector2 Bezier(Vector2 first, Vector2 a, Vector2 b, Vector2 last, float t)
        {
            float u = 1f - t;
            return u * u * u * first + 3f * u * u * t * a + 3f * u * t * t * b + t * t * t * last;
        }

        private static Vector2 GetOutward(Vector2 preceding, Vector2 point, Vector2 following)
        {
            Vector2 left = -Vector2.Perpendicular((point - preceding).normalized);
            Vector2 right = -Vector2.Perpendicular((following - point).normalized);
            Vector2 outward = (left + right).normalized;
            if (outward == Vector2.zero)
            {
                return right;
            }

            return outward / Mathf.Max(0.4f, Vector2.Dot(outward, right));
        }

        private static void DrawFill(VertexHelper mesh, ReadOnlySpan<Vert> points, Vector2 center, float aa, in Paint fill)
        {
            int origin = fill.Add(mesh, center);
            int previous = -1;
            int previousAa = -1;
            for (int i = 0; i < points.Length; i++)
            {
                Vert row = points[i];
                int index = fill.Add(mesh, row.Position);
                int indexAa = aa > 0f ? fill.Add(mesh, row.Position + row.SmoothingOutward * aa, 0f) : index;
                if (i > 0)
                {
                    GraphicMesh.AddFanTriangle(mesh, origin, previous, index);
                    GraphicMesh.AddStrip(mesh, previous, previousAa, index, indexAa);
                }

                previous = index;
                previousAa = indexAa;
            }
        }

        private static void DrawSolidStroke(VertexHelper mesh, ReadOnlySpan<Vert> points, Vector2 center, float thickness,
            float innerAa, float aa, bool shouldFill, in Paint fill, in Paint stroke)
        {
            int origin = shouldFill ? fill.Add(mesh, center) : -1;
            StrokeVert previous = default;
            for (int i = 0; i < points.Length; i++)
            {
                StrokeVert row = AddStrokeVert(mesh, points[i], center, thickness, innerAa, aa, shouldFill, fill, stroke);
                if (i > 0)
                {
                    LinkStroke(mesh, previous, row);
                    if (shouldFill)
                    {
                        GraphicMesh.AddFanTriangle(mesh, origin, previous.InnerAa, row.InnerAa);
                    }
                }

                previous = row;
            }
        }

        private static StrokeVert AddStrokeVert(VertexHelper mesh, in Vert point, Vector2 center,
            float thickness, float innerAa, float aa, bool shouldFill, in Paint fill, in Paint stroke, float opacity = 1f)
        {
            Vector2 position = point.Position;
            Vector2 inner = Inset(point, center, thickness);
            Vector2 innerEdge = Inset(point, center, thickness + innerAa);

            int innerIndex = stroke.Add(mesh, inner, opacity);
            int innerAaIndex = innerIndex;
            if (innerAa > 0f || shouldFill)
            {
                innerAaIndex = shouldFill ? fill.Add(mesh, innerEdge) : stroke.Add(mesh, innerEdge, 0f);
            }

            int outerIndex = stroke.Add(mesh, position, opacity);
            return new StrokeVert
            {
                Inner = innerIndex,
                InnerAa = innerAaIndex,
                Outer = outerIndex,
                OuterAa = aa > 0f ? stroke.Add(mesh, position + point.SmoothingOutward * aa, 0f) : outerIndex,
            };
        }

        private static void LinkStroke(VertexHelper mesh, in StrokeVert previous, in StrokeVert row)
        {
            GraphicMesh.AddStrip(mesh, previous.InnerAa, previous.Inner, row.InnerAa, row.Inner);
            GraphicMesh.AddStrip(mesh, previous.Inner, previous.Outer, row.Inner, row.Outer);
            GraphicMesh.AddStrip(mesh, previous.Outer, previous.OuterAa, row.Outer, row.OuterAa);
        }

        private static Vector2 Inset(in Vert point, Vector2 center, float depth)
        {
            float maximum = (point.Position - center).magnitude / Mathf.Max(1f, point.Outward.magnitude);
            return point.Position - point.Outward * Mathf.Min(maximum, depth);
        }

        private static float PrepareOuterSmoothing(Span<Vert> points, Vector2 center, float requested)
        {
            float safe = -LimitBandDepth(points, center, 0f, -requested);
            if (safe >= Mathf.Min(0.05f, requested * 0.1f))
            {
                return safe;
            }

            // At a needle-like valley the normal band can fold. Scaling the outline about its center
            // preserves the order of the samples and leaves a gradual fade even in that case.
            float largestRadiusSquared = 0f;
            foreach (var point in points)
            {
                largestRadiusSquared = Mathf.Max(largestRadiusSquared, (point.Position - center).sqrMagnitude);
            }

            if (largestRadiusSquared <= 0f)
            {
                return safe;
            }

            float inverseRadius = 1f / Mathf.Sqrt(largestRadiusSquared);
            foreach (ref var point in points)
            {
                point.SmoothingOutward = (point.Position - center) * inverseRadius;
            }

            return requested;
        }

        private static float LimitBandDepth(ReadOnlySpan<Vert> points, Vector2 center, float referenceDepth, float requestedDepth)
        {
            if (referenceDepth == requestedDepth || CanUseDepth(points, requestedDepth))
            {
                return requestedDepth;
            }

            float lower = 0f;
            float upper = 1f;
            // A very acute valley can leave less than a thousandth of a UI unit for the band.
            for (int i = 0; i < 20; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (CanUseDepth(points, Mathf.Lerp(referenceDepth, requestedDepth, middle)))
                {
                    lower = middle;
                }
                else
                {
                    upper = middle;
                }
            }

            return Mathf.Lerp(referenceDepth, requestedDepth, lower);

            bool CanUseDepth(ReadOnlySpan<Vert> outline, float depth)
            {
                return IsValidBand(outline, center, Mathf.Min(referenceDepth, depth), Mathf.Max(referenceDepth, depth));
            }
        }

        private static bool IsValidBand(ReadOnlySpan<Vert> points, Vector2 center, float outerDepth, float innerDepth)
        {
            const float tolerance = 0.00001f;
            Vector2 previousOuter = Inset(points[0], center, outerDepth);
            Vector2 previousInner = Inset(points[0], center, innerDepth);
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 outer = Inset(points[i], center, outerDepth);
                Vector2 inner = Inset(points[i], center, innerDepth);
                if (Cross(inner - previousInner, previousOuter - previousInner) > tolerance ||
                    Cross(outer - inner, previousOuter - inner) > tolerance ||
                    Cross(previousInner - center, inner - center) < -tolerance ||
                    Cross(previousOuter - center, outer - center) < -tolerance)
                {
                    return false;
                }

                previousOuter = outer;
                previousInner = inner;
            }

            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private void DrawDashes(VertexHelper mesh, ReadOnlySpan<Vert> points, Vector2 center, float thickness,
            float innerAa, float aa, in Paint stroke, float dashLength, float gapLength)
        {
            float total = points[^1].Distance;
            float period = dashLength + gapLength;
            int count = Mathf.Max(1, Mathf.RoundToInt(total / period));
            float step = total / count;
            float visibleLength = step * (dashLength / period);
            float phase = Mathf.Repeat(FiniteWidth(_dashPhase), 1f) * step;
            float feather = Mathf.Min(aa, visibleLength * 0.25f);
            for (int i = 0; i < count; i++)
            {
                float from = step * i + phase;
                DrawDash(mesh, points, center, thickness, innerAa, aa, stroke, from, from + visibleLength, feather);
            }
        }

        private static void DrawDash(VertexHelper mesh, ReadOnlySpan<Vert> points, Vector2 center, float thickness,
            float innerAa, float aa, in Paint stroke, float from, float to, float feather)
        {
            float total = points[^1].Distance;
            Vert first = AtDistance(points, from, out int nextIndex);
            StrokeVert previous = AddStrokeVert(mesh, first, center, thickness, innerAa, aa, false, default, stroke, feather > 0f ? 0f : 1f);
            float loopOffset = Mathf.Floor(from / total) * total;
            float nextDistance = points[nextIndex].Distance + loopOffset;
            bool startFeather = feather > 0f;
            bool endFeather = startFeather;
            while (nextDistance < to || startFeather || endFeather)
            {
                float position = nextDistance;
                if (startFeather && from + feather <= position)
                {
                    position = from + feather;
                    startFeather = false;
                }
                else if (endFeather && to - feather <= position)
                {
                    position = to - feather;
                    endFeather = false;
                }
                else
                {
                    nextIndex++;
                    if (nextIndex == points.Length)
                    {
                        nextIndex = 1;
                        loopOffset += total;
                    }
                    nextDistance = points[nextIndex].Distance + loopOffset;
                }

                float opacity = feather > 0f ? Mathf.Clamp01(Mathf.Min(position - from, to - position) / feather) : 1f;
                Vert vertex = AtDistance(points, position, out _);
                StrokeVert row = AddStrokeVert(mesh, vertex, center, thickness, innerAa, aa, false, default, stroke, opacity);
                LinkStroke(mesh, previous, row);
                previous = row;
            }

            Vert end = AtDistance(points, to, out _);
            StrokeVert last = AddStrokeVert(mesh, end, center, thickness, innerAa, aa, false, default, stroke, feather > 0f ? 0f : 1f);
            LinkStroke(mesh, previous, last);
        }

        private static Vert AtDistance(ReadOnlySpan<Vert> points, float distance, out int nextIndex)
        {
            distance = Mathf.Repeat(distance, points[^1].Distance);
            int lower = 0;
            int upper = points.Length - 1;
            while (upper - lower > 1)
            {
                int middle = (lower + upper) / 2;
                if (points[middle].Distance <= distance)
                {
                    lower = middle;
                }
                else
                {
                    upper = middle;
                }
            }

            nextIndex = upper;
            float t = Mathf.InverseLerp(points[lower].Distance, points[upper].Distance, distance);
            return new Vert
            {
                Position = Vector2.Lerp(points[lower].Position, points[upper].Position, t),
                Outward = Vector2.Lerp(points[lower].Outward, points[upper].Outward, t),
                SmoothingOutward = Vector2.Lerp(points[lower].SmoothingOutward, points[upper].SmoothingOutward, t),
                Distance = distance,
            };
        }

        private static float FiniteWidth(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float FiniteSignedUnit(float value)
        {
            return float.IsFinite(value) ? Mathf.Clamp(value, -1f, 1f) : 0f;
        }
    }
}
