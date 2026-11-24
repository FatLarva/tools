using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Rounded Rect Graphic", 12)]
    public class RoundedRectGraphic : PrimitiveImage
    {
        private enum GradientType
        {
            Horizontal,
            Vertical,
        }
        
        internal enum EdgeType
        {
            Fill = 0,
            Edge = 1,
            EdgeAndFill = 2,
        }
        
        internal enum EdgeThicknessType
        {
            Absolute,
            Relative,
        }
        
        [Serializable]
        private struct CornersRoundings
        {
            [Range(0, 0.5f)]
            public float BottomLeft;
            [Range(0, 0.5f)]
            public float TopLeft;
            [Range(0, 0.5f)]
            public float TopRight;
            [Range(0, 0.5f)]
            public float BottomRight;

            public CornersRoundings(float uniformRounding)
            {
                BottomLeft = uniformRounding;
                TopLeft = uniformRounding;
                TopRight = uniformRounding;
                BottomRight = uniformRounding;
            }
        }
        
        private struct Vert
        {
            public Vector2 Position;
            public Vector2 AaPosition;
            public Vector2 EdgePosition;
            public Vector2 AaEdgePosition;
            public float Distance;
        }

        private readonly struct BorderStyle
        {
            public readonly Rect Rect;
            public readonly Vector4 SpriteUv;
            public readonly int FillIndex;
            public readonly bool HasInnerAa;
            public readonly bool HasOuterAa;

            public BorderStyle(Rect rect, Vector4 spriteUv, int fillIndex, bool hasInnerAa, bool hasOuterAa)
            {
                Rect = rect;
                SpriteUv = spriteUv;
                FillIndex = fillIndex;
                HasInnerAa = hasInnerAa;
                HasOuterAa = hasOuterAa;
            }
        }

        [SerializeField] private bool _shouldUseGradient;

        [SerializeField] private Gradient _colorGradient = ClosedContour.WhiteGradient();

        [SerializeField] private GradientType _gradientType;

        [SerializeField, Range(0, 0.5f)] private float _cornerRadius = 0.25f;

        [SerializeField, Range(3, 36)] private int _cornerQuality = 10;
        
        [SerializeField]
        private bool _keepSquare = false;
        
        [SerializeField]
        private bool _separateRoundingForCorners = false;
        
        [SerializeField]
        private CornersRoundings _cornersRoundings;

        [SerializeField]
        private EdgeType _edgeType;
        
        [SerializeField]
        private EdgeThicknessType _edgeThicknessType;
        
        [SerializeField, Range(0, 100)]
        private float _edgeThickness = 0;
        
        [SerializeField, Range(0, 1)]
        private float _edgeThicknessRelative = 0;
        
        [SerializeField] private Color _edgeColor = Color.white;
        
        [SerializeField] private bool _shouldUseGradientForEdge;

        [SerializeField] private Gradient _edgeColorGradient = ClosedContour.WhiteGradient();

        [SerializeField] private GradientType _edgeGradientType;
        
        [SerializeField, Range(0, 100)]
        [Tooltip("Thickness of the line where edge color interpolates into mainColor")]
        private float _innerAaThickness = 0;
        
        [SerializeField, Range(0, 100)]
        [Tooltip("When greater than 0 adds thin mesh edge around generated mesh with alpha transitioning from 1 to 0 so it'll look smoother.")]
        private float _antiAliasingBorderThickness = 0;

        [SerializeField]
        private bool _isDashed;
        
        [SerializeField, Range(0, 1000)]
        [Tooltip("Desired dash length in local UI units. The pattern fits evenly around the perimeter.")]
        private float _dashLengthPixels = 12f;
        
        [SerializeField, Range(0, 1000)]
        [Tooltip("Gap length along the perimeter in local UI units. Zero produces a continuous outline.")]
        private float _dashGapLengthPixels = 8f;
        
        [SerializeField, Range(0, 1)]
        [Tooltip("Offset within one dash and gap period.")]
        private float _dashShift = 0;
        
        [SerializeField, UsedImplicitly]
        private int _vertCount;
        
        [SerializeField, UsedImplicitly]
        private int _trianglesCount;

        internal bool DashBudgetExceeded { get; private set; }

        public float CornerRadius
        {
            get => _cornerRadius;
            set
            {
                var radius = Mathf.Clamp(NonnegativeFinite(value), 0, 0.5f);
                if (_separateRoundingForCorners || _cornerRadius != radius)
                {
                    _separateRoundingForCorners = false;
                    _cornerRadius = radius;
                    SetVerticesDirty();
                }
            }
        }

        public float EdgeThicknessAbsolute
        {
            get => _edgeThickness;
            set
            {
                var thickness = NonnegativeFinite(value);
                if (_edgeThicknessType != EdgeThicknessType.Absolute || _edgeThickness != thickness)
                {
                    _edgeThicknessType = EdgeThicknessType.Absolute;
                    _edgeThickness = thickness;
                    SetVerticesDirty();
                }
            }
        }

        public float EdgeThicknessRelative
        {
            get => _edgeThicknessRelative;
            set
            {
                var thickness = Mathf.Clamp01(NonnegativeFinite(value));
                if (_edgeThicknessType != EdgeThicknessType.Relative || _edgeThicknessRelative != thickness)
                {
                    _edgeThicknessType = EdgeThicknessType.Relative;
                    _edgeThicknessRelative = thickness;
                    SetVerticesDirty();
                }
            }
        }

        public Color EdgeColor
        {
            get => _edgeColor;
            set
            {
                _edgeColor = value;
                SetVerticesDirty();
            }
        }
        
        public Color MainColor
        {
            get => color;
            set
            {
                color = value;
                SetVerticesDirty();
            }
        }
        
        public void SetColors(Color mainColor, Color edgeColor)
        {
            color = mainColor;
            _edgeColor = edgeColor;
            
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _vertCount = 0;
            _trianglesCount = 0;
            DashBudgetExceeded = false;

            var rect = GetPixelAdjustedRect();
            if (!float.IsFinite(rect.width) || !float.IsFinite(rect.height) ||
                !float.IsFinite(rect.x) || !float.IsFinite(rect.y))
            {
                return;
            }

            var smallerSide = Mathf.Min(rect.width, rect.height);
            if (smallerSide <= 0)
            {
                return;
            }

            var halfSide = smallerSide * 0.5f;
            if (_keepSquare)
            {
                rect = new Rect(rect.center - Vector2.one * halfSide, Vector2.one * smallerSide);
            }

            var thickness = _edgeThicknessType == EdgeThicknessType.Relative
                ? _edgeThicknessRelative * smallerSide
                : _edgeThickness;
            var edge = Mathf.Min(NonnegativeFinite(thickness), halfSide);
            var innerAa = Mathf.Min(NonnegativeFinite(_innerAaThickness), halfSide - edge);
            var outerAa = NonnegativeFinite(_antiAliasingBorderThickness);
            var radius = _separateRoundingForCorners ? _cornersRoundings : new CornersRoundings(_cornerRadius);
            var quality = Mathf.Clamp(_cornerQuality, 3, 36);
            var fill = _edgeType != EdgeType.Edge;
            var outline = _edgeType != EdgeType.Fill && edge > 0;
            var dash = NonnegativeFinite(_dashLengthPixels);
            var gap = NonnegativeFinite(_dashGapLengthPixels);
            var dashed = outline && _isDashed && gap > 0;
            var spriteUv = GraphicMesh.GetSpriteUv(overrideSprite);

            Span<Vert> storage = stackalloc Vert[4 * (quality + 3) + 1];
            var contour = BuildContour(storage, rect, radius, quality, edge, innerAa, outerAa, dashed);
            if (!float.IsFinite(contour[^1].Distance) || contour[^1].Distance <= 0)
            {
                return;
            }

            var periods = 0;
            if (dashed && dash > 0 && !TryGetDashPeriods(contour, dash, gap, fill, outerAa > 0, out periods))
            {
                contour = BuildContour(storage, rect, radius, 3, edge, innerAa, outerAa, true);
                DashBudgetExceeded = !TryGetDashPeriods(contour, dash, gap, fill, outerAa > 0, out periods);
            }

            if (outline && !dashed)
            {
                var style = new BorderStyle(rect, spriteUv, AddFillCenter(vh, rect, spriteUv, fill), innerAa > 0, outerAa > 0);
                DrawBorderSpan(vh, contour, style, 0, contour[^1].Distance, true);
            }
            else if (periods > 0)
            {
                var style = new BorderStyle(rect, spriteUv, AddFillCenter(vh, rect, spriteUv, fill), innerAa > 0, outerAa > 0);
                DrawDashedBorder(vh, contour, style, dash, gap, periods, outerAa);
            }
            else if (fill)
            {
                DrawFill(vh, contour, rect, spriteUv, outerAa > 0);
            }

            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }

        private static Span<Vert> BuildContour(Span<Vert> storage, Rect rect, CornersRoundings radii,
            int quality, float edge, float innerAa, float outerAa, bool alignDashCuts)
        {
            var minSide = Mathf.Min(rect.width, rect.height);
            var count = 0;
            AddCorner(storage, ref count, new Vector2(rect.xMin, rect.yMin), Vector2.left, Vector2.down,
                Mathf.Min(NonnegativeFinite(radii.BottomLeft), 0.5f) * minSide, quality, edge, innerAa, outerAa, alignDashCuts);
            AddCorner(storage, ref count, new Vector2(rect.xMax, rect.yMin), Vector2.down, Vector2.right,
                Mathf.Min(NonnegativeFinite(radii.BottomRight), 0.5f) * minSide, quality, edge, innerAa, outerAa, alignDashCuts);
            AddCorner(storage, ref count, new Vector2(rect.xMax, rect.yMax), Vector2.right, Vector2.up,
                Mathf.Min(NonnegativeFinite(radii.TopRight), 0.5f) * minSide, quality, edge, innerAa, outerAa, alignDashCuts);
            AddCorner(storage, ref count, new Vector2(rect.xMin, rect.yMax), Vector2.up, Vector2.left,
                Mathf.Min(NonnegativeFinite(radii.TopLeft), 0.5f) * minSide, quality, edge, innerAa, outerAa, alignDashCuts);

            var first = storage[0];
            var last = storage[count - 1];
            first.Distance = last.Distance + Vector2.Distance(last.Position, first.Position);
            if (alignDashCuts)
            {
                AddSideRows(storage, ref count, last, first);
            }

            storage[count] = first;
            return storage[..(count + 1)];
        }

        private static void AddCorner(Span<Vert> storage, ref int count, Vector2 corner, Vector2 firstNormal,
            Vector2 lastNormal, float radius, int quality, float edge, float innerAa, float outerAa, bool alignDashCuts)
        {
            var cornerDirection = firstNormal + lastNormal;
            var center = corner - cornerDirection * radius;
            var sampleCount = radius > 0 ? quality + 1 : 1;
            var arcStep = radius * Mathf.PI * 0.5f / quality;
            for (int i = 0; i < sampleCount; i++)
            {
                var normal = firstNormal;
                if (i == quality)
                {
                    normal = lastNormal;
                }
                else if (i > 0)
                {
                    var angle = i * Mathf.PI * 0.5f / quality;
                    normal = firstNormal * Mathf.Cos(angle) + lastNormal * Mathf.Sin(angle);
                }

                var position = center + normal * radius;
                var distance = 0f;
                if (count > 0)
                {
                    var previous = storage[count - 1];
                    var step = i == 0 ? Vector2.Distance(previous.Position, position) : arcStep;
                    distance = previous.Distance + step;
                }

                var row = new Vert
                {
                    Position = position,
                    AaPosition = radius > 0 ? center + normal * (radius + outerAa) : corner + cornerDirection * outerAa,
                    EdgePosition = OffsetCorner(corner, center, normal, cornerDirection, radius, edge),
                    AaEdgePosition = OffsetCorner(corner, center, normal, cornerDirection, radius, edge + innerAa),
                    Distance = distance,
                };
                if (alignDashCuts && i == 0 && count > 0)
                {
                    AddSideRows(storage, ref count, storage[count - 1], row);
                }

                storage[count++] = row;
            }
        }

        private static void AddSideRows(Span<Vert> storage, ref int count, Vert first, Vert last)
        {
            var side = last.Position - first.Position;
            var length = side.magnitude;
            if (length <= 0.0001f)
            {
                return;
            }

            var direction = side / length;
            var startInset = Mathf.Max(0f, Vector2.Dot(first.AaEdgePosition - first.Position, direction));
            var endInset = Mathf.Max(0f, Vector2.Dot(last.Position - last.AaEdgePosition, direction));
            var remaining = length - startInset - endInset;
            if (remaining < -0.0001f)
            {
                return;
            }

            var room = Mathf.Max(0f, remaining) * 0.5f;
            var from = startInset + Mathf.Min(room, Mathf.Max(0f, Vector2.Dot(first.Position - first.AaPosition, direction) - startInset));
            var to = length - endInset - Mathf.Min(room, Mathf.Max(0f, Vector2.Dot(last.AaPosition - last.Position, direction) - endInset));
            // Keep miter movement near the corner. Between these rows every layer has the same coordinate along the side.
            if (from > 0.0001f && from < length - 0.0001f)
            {
                storage[count++] = ProjectSideRow(first, direction, from);
            }

            if (to > from + 0.0001f && to < length - 0.0001f)
            {
                storage[count++] = ProjectSideRow(first, direction, to);
            }
        }

        private static Vert ProjectSideRow(Vert first, Vector2 direction, float distance)
        {
            var position = first.Position + direction * distance;
            return new Vert
            {
                Position = position,
                AaPosition = first.AaPosition + direction * Vector2.Dot(position - first.AaPosition, direction),
                EdgePosition = first.EdgePosition + direction * Vector2.Dot(position - first.EdgePosition, direction),
                AaEdgePosition = first.AaEdgePosition + direction * Vector2.Dot(position - first.AaEdgePosition, direction),
                Distance = first.Distance + distance,
            };
        }

        private static Vector2 OffsetCorner(Vector2 corner, Vector2 center, Vector2 normal,
            Vector2 cornerDirection, float radius, float inset)
        {
            if (inset >= radius)
            {
                return corner - cornerDirection * inset;
            }

            return center + normal * (radius - inset);
        }

        private static bool TryGetDashPeriods(ReadOnlySpan<Vert> contour, float dash, float gap,
            bool fill, bool fade, out int periods)
        {
            var count = Math.Max(1d, Math.Round(contour[^1].Distance / ((double)dash + gap)));
            // Each dash contributes two endpoints and up to two fade rows. Gaps contribute two endpoints.
            // The final interval includes any part of the pattern that wraps across the start of the contour.
            var rows = contour.Length + (count + 1) * (2 + (fade ? 2 : 0) + (fill ? 2 : 0));
            var vertices = rows * (fade ? 4 : 3) + (fill ? 1 : 0);
            if (vertices > GraphicMesh.MaximumVertices)
            {
                periods = 0;
                return false;
            }

            periods = (int)count;
            return true;
        }

        private void DrawDashedBorder(VertexHelper vh, ReadOnlySpan<Vert> contour, BorderStyle style,
            float dash, float gap, int periods, float outerAa)
        {
            var perimeter = contour[^1].Distance;
            var period = (double)perimeter / periods;
            var dashLength = period * (dash / ((double)dash + gap));
            var phase = Mathf.Repeat(NonnegativeFinite(_dashShift), 1f) * period;
            var fade = Mathf.Min(outerAa, (float)(dashLength * 0.5d));
            for (int i = 0; i <= periods; i++)
            {
                var start = i * period - phase;
                var end = start + dashLength;
                var from = (float)Math.Max(0, start);
                var to = (float)Math.Min(perimeter, end);
                if (to > from)
                {
                    DrawBorderSpan(vh, contour, style, from, to, true, (float)start, (float)end, fade);
                }

                if (style.FillIndex >= 0)
                {
                    var gapFrom = (float)Math.Max(0, end);
                    var gapTo = (float)Math.Min(perimeter, start + period);
                    if (gapTo > gapFrom)
                    {
                        DrawBorderSpan(vh, contour, style, gapFrom, gapTo, false);
                    }
                }
            }
        }

        private void DrawBorderSpan(VertexHelper vh, ReadOnlySpan<Vert> contour, BorderStyle style,
            float from, float to, bool drawEdge, float dashStart = 0f, float dashEnd = 0f, float fade = 0f)
        {
            var current = from;
            var next = FindNextSample(contour, current);
            var fullStart = dashStart + fade;
            var fullEnd = dashEnd - fade;
            var previous = AddBorderRow(vh, SampleContour(contour, next, current), style,
                GetDashCoverage(current, drawEdge, dashStart, dashEnd, fade));
            while (current < to)
            {
                var distance = Mathf.Min(to, contour[next].Distance);
                if (fade > 0f && current < fullStart)
                {
                    distance = Mathf.Min(distance, fullStart);
                }
                else if (fade > 0f && current < fullEnd)
                {
                    distance = Mathf.Min(distance, fullEnd);
                }

                var blend = GetDashCoverage(distance, drawEdge, dashStart, dashEnd, fade);
                var row = AddBorderRow(vh, SampleContour(contour, next, distance), style, blend);
                ConnectRows(vh, style, previous, row);
                previous = row;
                current = distance;
                while (next < contour.Length - 1 && contour[next].Distance <= current)
                {
                    next++;
                }
            }
        }

        private static float GetDashCoverage(float distance, bool drawEdge, float dashStart, float dashEnd, float fade)
        {
            if (!drawEdge)
            {
                return 0f;
            }

            return fade > 0f ? Mathf.Clamp01(Mathf.Min(distance - dashStart, dashEnd - distance) / fade) : 1f;
        }

        private static int FindNextSample(ReadOnlySpan<Vert> contour, float distance)
        {
            var lower = 1;
            var upper = contour.Length - 1;
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (contour[middle].Distance <= distance)
                {
                    lower = middle + 1;
                }
                else
                {
                    upper = middle;
                }
            }

            return lower;
        }

        private static Vert SampleContour(ReadOnlySpan<Vert> contour, int next, float distance)
        {
            var a = contour[next - 1];
            var b = contour[next];
            var t = Mathf.InverseLerp(a.Distance, b.Distance, distance);
            return new Vert
            {
                Position = Vector2.LerpUnclamped(a.Position, b.Position, t),
                AaPosition = Vector2.LerpUnclamped(a.AaPosition, b.AaPosition, t),
                EdgePosition = Vector2.LerpUnclamped(a.EdgePosition, b.EdgePosition, t),
                AaEdgePosition = Vector2.LerpUnclamped(a.AaEdgePosition, b.AaEdgePosition, t),
                Distance = distance,
            };
        }

        private int AddBorderRow(VertexHelper vh, Vert vert, BorderStyle style, float edgeBlend)
        {
            var rect = style.Rect;
            var fill = style.FillIndex >= 0;
            var edgeColor = CalculateColor(vert.Position, rect, _shouldUseGradientForEdge, _edgeColor,
                _edgeGradientType, _edgeColorGradient);
            var transparent = new Color32(edgeColor.r, edgeColor.g, edgeColor.b, 0);
            var innerAa = fill ? FillColor(vert.AaEdgePosition, rect) : transparent;
            var edgeFill = fill ? FillColor(vert.EdgePosition, rect) : transparent;
            var outerFill = fill ? FillColor(vert.Position, rect) : transparent;
            var inner = Color32.Lerp(edgeFill, edgeColor, edgeBlend);
            var outer = Color32.Lerp(outerFill, edgeColor, edgeBlend);
            var first = vh.currentVertCount;
            AddVertex(vh, vert.AaEdgePosition, innerAa, rect, style.SpriteUv);
            AddVertex(vh, vert.EdgePosition, inner, rect, style.SpriteUv);
            AddVertex(vh, vert.Position, outer, rect, style.SpriteUv);
            if (style.HasOuterAa)
            {
                AddVertex(vh, vert.AaPosition, new Color32(outer.r, outer.g, outer.b, 0), rect, style.SpriteUv);
            }

            return first;
        }

        private static void ConnectRows(VertexHelper vh, BorderStyle style, int previous, int row)
        {
            if (style.FillIndex >= 0)
            {
                GraphicMesh.AddFanTriangle(vh, style.FillIndex, previous, row);
            }

            if (style.HasInnerAa)
            {
                GraphicMesh.AddStrip(vh, previous, previous + 1, row, row + 1);
            }

            GraphicMesh.AddStrip(vh, previous + 1, previous + 2, row + 1, row + 2);
            if (style.HasOuterAa)
            {
                GraphicMesh.AddStrip(vh, previous + 2, previous + 3, row + 2, row + 3);
            }
        }

        private void DrawFill(VertexHelper vh, ReadOnlySpan<Vert> contour, Rect rect, Vector4 spriteUv, bool hasAa)
        {
            var count = contour.Length - 1;
            var center = AddFillCenter(vh, rect, spriteUv, count != 4);
            var start = vh.currentVertCount;
            var step = hasAa ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var vert = contour[i];
                var fillColor = FillColor(vert.Position, rect);
                AddVertex(vh, vert.Position, fillColor, rect, spriteUv);
                if (hasAa)
                {
                    AddVertex(vh, vert.AaPosition, new Color32(fillColor.r, fillColor.g, fillColor.b, 0), rect, spriteUv);
                }
            }

            if (center < 0)
            {
                vh.AddTriangle(start, start + step * 2, start + step);
                vh.AddTriangle(start, start + step * 3, start + step * 2);
            }

            for (int i = 0; i < count; i++)
            {
                var previous = start + i * step;
                var current = start + (i + 1) % count * step;
                if (center >= 0)
                {
                    GraphicMesh.AddFanTriangle(vh, center, previous, current);
                }

                if (hasAa)
                {
                    GraphicMesh.AddStrip(vh, previous, previous + 1, current, current + 1);
                }
            }
        }

        private int AddFillCenter(VertexHelper vh, Rect rect, Vector4 spriteUv, bool fill)
        {
            if (!fill)
            {
                return -1;
            }

            var index = vh.currentVertCount;
            AddVertex(vh, rect.center, FillColor(rect.center, rect), rect, spriteUv);
            return index;
        }

        private Color32 FillColor(Vector2 position, Rect rect)
        {
            return CalculateColor(position, rect, _shouldUseGradient, color, _gradientType, _colorGradient);
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, Color32 color, Rect rect, Vector4 spriteUv)
        {
            vh.AddVert(position, color, GraphicMesh.MapUv(position, rect, spriteUv));
        }

        private static float NonnegativeFinite(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(value, 0) : 0f;
        }
#if UNITY_EDITOR
        // If the component changes in the inspector, update the mesh
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        private static Color32 CalculateColor(in Vector2 cornerPosition, in Rect enclosingRect, bool shouldUseGradient,
            Color singleColor, GradientType gradientType, Gradient gradient)
        {
            if (!shouldUseGradient || gradient == null)
            {
                return singleColor;
            }

            switch (gradientType)
            {
                case GradientType.Horizontal:
                {
                    var t = (cornerPosition.x - enclosingRect.xMin) / enclosingRect.width;
                    var clampedT = Mathf.Clamp01(t);
                    
                    return gradient.Evaluate(clampedT);
                }
                case GradientType.Vertical:
                {
                    var t = (cornerPosition.y - enclosingRect.yMin) / enclosingRect.height;
                    var clampedT = Mathf.Clamp01(t);
                    
                    return gradient.Evaluate(clampedT);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(gradientType), gradientType, "Unknown gradient type used.");
            }
        }

    }
}
