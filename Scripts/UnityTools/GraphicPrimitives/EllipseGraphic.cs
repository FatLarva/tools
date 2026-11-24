using System;
using JetBrains.Annotations;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Ellipse Graphic", 12)]
    public class EllipseGraphic : PrimitiveImage
    {
        private const int RoundCapSections = 10;
        private const float MinimumDashPeriod = 0.0001f;
        private const float MaximumAaMiter = 4f;
        
        private struct Vert
        {
            public Vector2 Position;
            public Vector2 AaPosition;
            public Vector2 EdgePosition;
            public Vector2 AaEdgePosition;
            public int Index;
            public int AaIndex;
            public int EdgeIndex;
            public int AaEdgeIndex;
        }

        private struct SegmentVert
        {
            public Vector2 Position;
            public Vector2 AaPosition;
            public int Index;
            public int AaIndex;
            public bool AaOnIncomingEdge;
        }

        private readonly struct DashPattern
        {
            public readonly int Count;
            public readonly float Period;
            public readonly float Gap;
            public readonly bool IsContinuous;

            public DashPattern(int count, float period, float gap, bool isContinuous = false)
            {
                Count = count;
                Period = period;
                Gap = gap;
                IsContinuous = isContinuous;
            }
        }
        
        private readonly ref struct ColorCalculationData
        {
            public readonly Rect EnclosingRect;
            public readonly Color SingleColor;
            public readonly Gradient GradientColor;
            public readonly GradientType GradientType;
            public readonly bool ShouldUseGradient;

            public ColorCalculationData(Rect enclosingRect, Color singleColor, Gradient gradientColor, GradientType gradientType, bool shouldUseGradient)
            {
                EnclosingRect = enclosingRect;
                SingleColor = singleColor;
                GradientColor = gradientColor;
                GradientType = gradientType;
                ShouldUseGradient = shouldUseGradient;
            }
        }
        
        private readonly ref struct EllipseInfo
        {
            public readonly Rect EnclosingRect;
            public readonly Vector2 EllipseHalfSize;
            public readonly Vector2 EllipseAaHalfSize;
            public readonly Vector2 EllipseEdgeHalfSize;
            public readonly Vector2 EllipseEdgeAaHalfSize;
            public readonly float StartAngleRadians;
            public readonly float AngleDeltaRadians;
            public readonly int HullSections;
            public readonly int HullVertCount;
            public readonly int FullHullSections;
            public readonly bool IsAaEnabled;
            public readonly bool IsDashed;
            public readonly bool IsClosed;
            public readonly bool IsClockwise;
            
            public EllipseInfo(Rect enclosingRect, int hullSections, float startAngle, float sweepAngle, bool isDashed, float aaWidth, float edgeWidth, float innerAaWidth)
            {
                EnclosingRect = enclosingRect;
                IsClosed = Mathf.Abs(sweepAngle) == 360;
                IsClockwise = sweepAngle < 0;
                FullHullSections = hullSections;
                HullSections = Mathf.Max(1, Mathf.CeilToInt(hullSections * (Mathf.Abs(sweepAngle) / 360)));
                HullVertCount = IsClosed ? HullSections : HullSections + 1;
                // Build both directions with the same triangle winding.
                StartAngleRadians = (IsClockwise && !IsClosed ? startAngle + sweepAngle : startAngle) * Mathf.Deg2Rad;
                AngleDeltaRadians = Mathf.Abs(sweepAngle) * Mathf.Deg2Rad / HullSections;
                IsAaEnabled = aaWidth > 0;
                IsDashed = isDashed;
                
                EllipseHalfSize = enclosingRect.size * 0.5f;
                EllipseAaHalfSize = EllipseHalfSize + Vector2.one * aaWidth;
                EllipseEdgeHalfSize = EllipseHalfSize - Vector2.one * edgeWidth;
                EllipseEdgeAaHalfSize = EllipseEdgeHalfSize - Vector2.one * innerAaWidth;
            }
        }
        
        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }

        public enum Mode
        {
            Fill = 0,
            Edge = 2,
            EdgeAndFill = 3,
            SegmentedFill = 4,
        };

        internal enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        };

        public enum DashMode
        {
            Angles,
            Count,
        }

        public enum SegmentLayout
        {
            DistributeSweep,
            FixedSlots,
        }

        [FormerlySerializedAs("startAngle")]
        [SerializeField, Range(0.0f, 360.0f)]
        private float _startAngle;

        [SerializeField, Range(-360.0f, 360.0f)]
        [Tooltip("Sweep in degrees from Start Angle. Negative values draw clockwise. Zero draws nothing.")]
        private float _sweepAngle = 360;
        
        [FormerlySerializedAs("detail")]
        [SerializeField, Min(3)]
        private int _hullSections = 40;
        
        [FormerlySerializedAs("keepCircle")]
        [SerializeField]
        private bool _keepCircle = false;

        [FormerlySerializedAs("mode")]
        [SerializeField]
        private Mode _mode;

        [SerializeField, Min(2)]
        [Tooltip("Number of filled slices across the sweep or around the full circle, depending on the layout.")]
        private int _segmentCount = 8;

        [SerializeField]
        private SegmentLayout _segmentLayout;

        [SerializeField]
        [Tooltip("Number of visible slots from Start Angle. Negative is counterclockwise. Positive is clockwise. A fraction reveals part of the last slot.")]
        private float _visibleSegments = 8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Inner ellipse size as a proportion of the outer ellipse.")]
        private float _innerRadius = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Width between neighboring solid slice edges, in local UI units. The AA fringe is excluded.")]
        private float _segmentGap = 3f;
        
        [FormerlySerializedAs("edgeThicknessMode")]
        [SerializeField]
        private EdgeThicknessMode _edgeThicknessMode;

        [FormerlySerializedAs("edgeThickness")]
        [SerializeField, Min(0)]
        [Tooltip("Edge mode only")]
        private float _edgeThickness = 1;
        
        [FormerlySerializedAs("edgeThicknessRelative")]
        [SerializeField, Range(0, 1)]
        [Tooltip("Edge mode only")]
        private float _edgeThicknessRelative = 0;
        
        [FormerlySerializedAs("isDashed")]
        [SerializeField]
        [Tooltip("Edge mode only")]
        private bool _isDashed = false;

        [SerializeField]
        private DashMode _dashMode = DashMode.Count;

        [SerializeField, Min(0)]
        [Tooltip("Visible dash length in degrees, independent of Hull Sections. Used by Angles mode.")]
        private float _dashAngle = 9;

        [SerializeField, Min(0)]
        [Tooltip("Visible angular gap between cap edges in degrees, excluding the transparent AA fringe.")]
        private float _gapAngle = 9;

        [SerializeField, Min(1)]
        [Tooltip("Number of equally spaced dashes across the entire Sweep Angle. Used by Count mode.")]
        private int _dashCount = 20;

        [SerializeField, HideInInspector]
        private int _dashSettingsVersion;

        [SerializeField]
        [Tooltip("Round the ends of an open edge or each dash.")]
        private bool _roundCaps;
        
        [FormerlySerializedAs("antiAliasingBorderThickness")]
        [SerializeField, Range(0, 100)]
        [Tooltip("When greater than 0 adds thin mesh edge around generated mesh with alpha transitioning from 1 to 0 so it'll look smoother.")]
        private float _antiAliasingLayerWidth = 0;
        
        [FormerlySerializedAs("_innerAaThickness")]
        [SerializeField, Range(0, 100)]
        [Tooltip("Thickness of the line where edge color interpolates into mainColor")]
        private float _innerAntiAliasingLayerWidth = 0;
        
        [SerializeField]
        private bool _shouldUseGradient;
        
        [SerializeField]
        private GradientType _gradientType;
        
        [SerializeField]
        private Gradient _gradientColor = ClosedContour.WhiteGradient();
        
        [FormerlySerializedAs("edgeColor")]
        [SerializeField]
        private Color _edgeColor = Color.white;
        
        [SerializeField]
        private bool _shouldUseGradientForEdge;

        [SerializeField]
        private Gradient _edgeColorGradient = ClosedContour.WhiteGradient();

        [SerializeField]
        private GradientType _edgeGradientType;
        
        [SerializeField, UsedImplicitly]
        private int _vertCount;
        
        [SerializeField, UsedImplicitly]
        private int _trianglesCount;

        internal string MeshDiagnostic { get; private set; }
        private Vector4 _spriteUv;
        
        protected override void OnEnable()
        {
            InitializeDashSettings();
            base.OnEnable();
        }
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            InitializeDashSettings();
            _startAngle = NormalizeStartAngle(_startAngle);
            _sweepAngle = ClampSweep(_sweepAngle);
            _segmentCount = Mathf.Max(2, _segmentCount);
            _visibleSegments = ClampVisibleSegments(_visibleSegments, _segmentCount);
            _innerRadius = Mathf.Clamp01(NonnegativeFinite(_innerRadius));
            _segmentGap = NonnegativeFinite(_segmentGap);
            _dashAngle = ClampAngle(_dashAngle);
            _gapAngle = ClampAngle(_gapAngle);
            _dashCount = Mathf.Max(1, _dashCount);
            base.OnValidate();
        }
#endif
        public bool IsDashed
        {
            get => _isDashed;
            set
            {
                InitializeDashSettings();
                if (_isDashed == value) return;
                _isDashed = value;
                SetVerticesDirty();
            }
        }

        public DashMode DashesMode => _dashMode;
        public float DashAngle => _dashAngle;
        public float GapAngle => _gapAngle;
        public int DashCount => _dashCount;

        public void SetDashesByCount(int count, float gapAngle)
        {
            count = Mathf.Max(1, count);
            gapAngle = ClampAngle(gapAngle);
            _dashSettingsVersion = 1;
            if (_isDashed && _dashMode == DashMode.Count && _dashCount == count && _gapAngle == gapAngle) return;
            _isDashed = true;
            _dashMode = DashMode.Count;
            _dashCount = count;
            _gapAngle = gapAngle;
            SetVerticesDirty();
        }

        public void SetDashesByAngle(float dashAngle, float gapAngle)
        {
            dashAngle = ClampAngle(dashAngle);
            gapAngle = ClampAngle(gapAngle);
            _dashSettingsVersion = 1;
            if (_isDashed && _dashMode == DashMode.Angles && _dashAngle == dashAngle && _gapAngle == gapAngle) return;
            _isDashed = true;
            _dashMode = DashMode.Angles;
            _dashAngle = dashAngle;
            _gapAngle = gapAngle;
            SetVerticesDirty();
        }

        private void InitializeDashSettings()
        {
            if (_dashSettingsVersion != 0) return;
            if (_isDashed)
            {
                // Old assets alternated mesh sections. Preserve their approximate density once,
                // then changing mesh quality no longer changes the pattern.
                var sweep = Mathf.Abs(ClampSweep(_sweepAngle));
                var sections = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(3, _hullSections) * sweep / 360));
                _dashMode = DashMode.Count;
                _dashCount = Mathf.Max(1, Mathf.RoundToInt(sections * 0.5f));
                _dashAngle = _gapAngle = sweep / sections;
            }
            _dashSettingsVersion = 1;
        }

        private static float ClampAngle(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, 0, 360);

        private static float ClampSweep(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, -360, 360);

        private static float NormalizeStartAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            if (value >= 0f && value <= 360f)
            {
                return value;
            }

            var wrapped = value % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        private static float NonnegativeFinite(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
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

        public Mode DrawingMode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                SetVerticesDirty();
            }
        }

        public int SegmentCount => _segmentCount;
        public float InnerRadius => _innerRadius;
        public float SegmentGap => _segmentGap;

        public SegmentLayout SegmentedFillLayout
        {
            get => _segmentLayout;
            set
            {
                if (_segmentLayout == value)
                {
                    return;
                }

                _segmentLayout = value;
                SetVerticesDirty();
            }
        }

        public float VisibleSegments
        {
            get => _visibleSegments;
            set
            {
                var visible = ClampVisibleSegments(value, Mathf.Max(2, _segmentCount));
                if (_visibleSegments == visible)
                {
                    return;
                }

                _visibleSegments = visible;
                SetVerticesDirty();
            }
        }

        public void SetSegments(int count, float innerRadius, float gap)
        {
            count = Mathf.Max(2, count);
            innerRadius = Mathf.Clamp01(NonnegativeFinite(innerRadius));
            gap = NonnegativeFinite(gap);
            if (_mode == Mode.SegmentedFill && _segmentCount == count && _innerRadius == innerRadius && _segmentGap == gap)
            {
                return;
            }

            _mode = Mode.SegmentedFill;
            _segmentCount = count;
            _visibleSegments = ClampVisibleSegments(_visibleSegments, count);
            _innerRadius = innerRadius;
            _segmentGap = gap;
            SetVerticesDirty();
        }

        public void SetVisibleSegments(float visibleSegments)
        {
            var visible = ClampVisibleSegments(visibleSegments, Mathf.Max(2, _segmentCount));
            if (_mode == Mode.SegmentedFill && _segmentLayout == SegmentLayout.FixedSlots && _visibleSegments == visible)
            {
                return;
            }

            _mode = Mode.SegmentedFill;
            _segmentLayout = SegmentLayout.FixedSlots;
            _visibleSegments = visible;
            SetVerticesDirty();
        }

        private static float ClampVisibleSegments(float visible, int count)
        {
            return float.IsFinite(visible) ? Mathf.Clamp(visible, -count, count) : 0f;
        }

        public float StartAngle
        {
            get => _startAngle;
            set
            {
                SetArc(value, _sweepAngle);
            }
        }

        public float SweepAngle
        {
            get => _sweepAngle;
            set
            {
                SetArc(_startAngle, value);
            }
        }

        public void SetArc(float startAngle, float sweepAngle)
        {
            startAngle = NormalizeStartAngle(startAngle);
            sweepAngle = ClampSweep(sweepAngle);
            if (_startAngle == startAngle && _sweepAngle == sweepAngle)
            {
                return;
            }

            _startAngle = startAngle;
            _sweepAngle = sweepAngle;
            SetVerticesDirty();
        }
        
        public void SetColors(Color newMainColor, Color newEdgeColor)
        {
            color = newMainColor;
            _edgeColor = newEdgeColor;
            
            SetVerticesDirty();
        }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            InitializeDashSettings();
            _vertCount = 0;
            _trianglesCount = 0;
            MeshDiagnostic = null;
            vh.Clear();
            
            Rect enclosingRect = GetPixelAdjustedRect();
            
            var smallerSide = Math.Min(enclosingRect.width, enclosingRect.height);
            var sweepAngle = ClampSweep(_sweepAngle);
            var usesFixedSlots = _mode == Mode.SegmentedFill && _segmentLayout == SegmentLayout.FixedSlots;
            if (smallerSide <= 0 || (sweepAngle == 0 && !usesFixedSlots))
            {
                return;
            }

            _spriteUv = GraphicMesh.GetSpriteUv(overrideSprite);

            var halfSmallerSide = smallerSide * 0.5f;
            if (_keepCircle)
            {
                enclosingRect = new Rect(enclosingRect.center - new Vector2(halfSmallerSide, halfSmallerSide), new Vector2(smallerSide, smallerSide));
            }

            var outerAa = NonnegativeFinite(_antiAliasingLayerWidth);
            if (_mode == Mode.SegmentedFill)
            {
                GenerateSegmentedFill(vh, enclosingRect, sweepAngle, outerAa);
                UpdateMeshCounts(vh);
                return;
            }

            var edge = _edgeThicknessMode switch
            {
                EdgeThicknessMode.Absolute => _edgeThickness,
                EdgeThicknessMode.Relative => _edgeThicknessRelative * halfSmallerSide,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(EdgeThicknessMode)}: {_edgeThicknessMode}"),
            };

            edge = Mathf.Min(NonnegativeFinite(edge), halfSmallerSide);
            var innerAa = Mathf.Min(NonnegativeFinite(_innerAntiAliasingLayerWidth), halfSmallerSide - edge);
            var pattern = GetDashPattern(Mathf.Abs(sweepAngle));
            var hullSections = FitHullSections(Mathf.Abs(sweepAngle), outerAa, innerAa, in pattern);
            if (hullSections == 0)
            {
                return;
            }

            var ellipseInfo = new EllipseInfo(
                enclosingRect,
                hullSections,
                NormalizeStartAngle(_startAngle),
                sweepAngle,
                _isDashed,
                outerAa,
                edge,
                innerAa);
            
            switch (_mode)
            {
                case Mode.Fill:
                {
                    GenerateFill(vh, in ellipseInfo);
                    break;
                }
                case Mode.Edge:
                case Mode.EdgeAndFill:
                {
                    GenerateEdge(vh, in ellipseInfo, _mode == Mode.EdgeAndFill, in pattern);
                    break;
                }
            }
            
            UpdateMeshCounts(vh);
        }

        private void UpdateMeshCounts(VertexHelper vh)
        {
            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }

        private DashPattern GetDashPattern(float sweep)
        {
            if (!_isDashed)
            {
                return default;
            }

            var gap = ClampAngle(_gapAngle);
            var count = Mathf.Max(1, _dashCount);
            var period = _dashMode == DashMode.Count ? sweep / count : ClampAngle(_dashAngle) + gap;
            if (period <= MinimumDashPeriod || gap >= period)
            {
                return default;
            }

            if (gap == 0)
            {
                return new DashPattern(1, sweep, 0f, sweep == 360f);
            }

            if (_dashMode == DashMode.Angles)
            {
                count = Mathf.CeilToInt(sweep / period);
            }

            return new DashPattern(count, period, gap);
        }

        private int FitHullSections(float sweep, float outerAa, float innerAa, in DashPattern pattern)
        {
            var closed = sweep == 360f;
            var hasAa = outerAa > 0f;
            var requested = Mathf.Max(3, _hullSections);
            int verticesPerSample;
            long reserved;
            if (_mode == Mode.Fill)
            {
                verticesPerSample = hasAa ? 2 : 1;
                reserved = closed ? 1 : 1 + verticesPerSample + (hasAa ? 4 : 0);
            }
            else
            {
                const int gradientEndpoints = 2;
                var stopCount = gradientEndpoints;
                if (_shouldUseGradientForEdge && _edgeGradientType == GradientType.Radial)
                {
                    stopCount += _edgeColorGradient.colorKeys.Length + _edgeColorGradient.alphaKeys.Length;
                }

                var rowSize = stopCount + 1 + (hasAa ? 1 : 0);
                var capSize = _roundCaps ? (stopCount + RoundCapSections - 1) * (outerAa > 0f || innerAa > 0f ? 3 : 2)
                    : hasAa ? stopCount + 2 : 0;
                var hasFill = _mode == Mode.EdgeAndFill;
                if (!_isDashed || pattern.IsContinuous)
                {
                    verticesPerSample = rowSize;
                    reserved = (hasFill ? 1 : 0) + (closed ? 0 : rowSize + 2 * capSize + (hasFill && hasAa ? 6 : 0));
                }
                else
                {
                    // Every dash has its own endpoint rows and caps. The combined arc length
                    // is no larger than the sweep, independently of the number of dashes.
                    verticesPerSample = (hasFill ? rowSize : 0) + (pattern.Count > 0 ? rowSize : 0);
                    reserved = hasFill ? 1 + (closed ? 0 : rowSize + (hasAa ? 6 : 0)) : 0;
                    reserved += (long)pattern.Count * (2 * rowSize + 2 * capSize);
                }
            }

            if (verticesPerSample == 0)
            {
                return 3;
            }

            // Leave one row for rounding the angular section count.
            var availableSamples = (GraphicMesh.MaximumVertices - reserved) / verticesPerSample - 1;
            if (availableSamples < 1)
            {
                MeshDiagnostic = "The dash pattern exceeds the mesh vertex budget. Reduce Dash Count or increase the dash and gap angles.";
                return 0;
            }

            var fitted = (int)Math.Min(GraphicMesh.MaximumVertices, Math.Floor(availableSamples * 360d / sweep));
            if (fitted < 3)
            {
                MeshDiagnostic = "The outline exceeds the mesh vertex budget. Reduce the number of dashes.";
                return 0;
            }

            if (fitted < requested)
            {
                MeshDiagnostic = "Hull Sections was reduced for this mesh to fit the vertex budget.";
            }

            return Mathf.Min(requested, fitted);
        }

        private void GenerateSegmentedFill(VertexHelper vh, Rect rect, float sweepAngle, float aaWidth)
        {
            var divisions = Mathf.Max(2, _segmentCount);
            var fixedSlots = _segmentLayout == SegmentLayout.FixedSlots;
            var visible = ClampVisibleSegments(_visibleSegments, divisions);
            var visibleMagnitude = Math.Min(divisions, Math.Abs((double)visible));
            var count = fixedSlots ? (int)Math.Ceiling(visibleMagnitude) : divisions;
            var radius = Mathf.Clamp01(NonnegativeFinite(_innerRadius));
            if (count == 0 || radius == 1f)
            {
                return;
            }

            var hasAa = aaWidth > 0f;
            var hasPartialSlot = fixedSlots && visibleMagnitude != Math.Floor(visibleMagnitude);
            var fullSlots = count - (hasPartialSlot ? 1 : 0);
            var verticesPerSample = (long)fullSlots * (hasAa ? 4 : 2);
            if (hasPartialSlot)
            {
                // Clip at most two triangles per sample and each to at most four
                // vertices. The clipped AA contour cannot exceed twice its input.
                verticesPerSample += hasAa ? 16 : 8;
            }

            var sampleLimit = (int)(GraphicMesh.MaximumVertices / verticesPerSample);
            if (sampleLimit < 3)
            {
                MeshDiagnostic = "The visible slices exceed the mesh vertex budget. Reduce the number of filled slices.";
                return;
            }

            var sweep = fixedSlots ? 360f : Mathf.Abs(sweepAngle);
            var requested = Math.Max(3, Math.Ceiling(Mathf.Max(3, _hullSections) * ((double)sweep / (360d * divisions))) + 1);
            var samples = (int)Math.Min(sampleLimit, requested);
            if (samples < requested)
            {
                MeshDiagnostic = "Hull Sections was reduced for this mesh to fit the vertex budget.";
            }

            using var storage = SpanBufferSharedPool.RentBuffer(samples * 2, out SpanBuffer<SegmentVert> buffer);
            var colors = new ColorCalculationData(rect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var halfSize = rect.size * 0.5f;
            var step = sweep * (double)Mathf.Deg2Rad / divisions;
            var start = (NormalizeStartAngle(_startAngle) + (fixedSlots ? 0f : Mathf.Min(0f, sweepAngle))) * (double)Mathf.Deg2Rad;
            var clockwise = fixedSlots && visible > 0f;
            var halfGap = NonnegativeFinite(_segmentGap) * 0.5f;
            var isOpen = fixedSlots ? visibleMagnitude < divisions : sweep < 360f;
            for (int i = 0; i < count; i++)
            {
                var first = (float)(start + (clockwise ? -(i + 1) : i) * step);
                var last = (float)(start + (clockwise ? -i : i + 1) * step);
                buffer.Reset();
                if (!FillSegmentVertices(ref buffer, rect.center, halfSize, radius, first, last, samples, halfGap))
                {
                    continue;
                }

                var vertices = buffer.FilledBufferSlice();
                var startAa = halfGap > 0f || (isOpen && i == (clockwise ? count - 1 : 0));
                var endAa = halfGap > 0f || (isOpen && i == (clockwise ? 0 : count - 1));
                if (hasPartialSlot && i == fullSlots)
                {
                    var cutAngle = (float)(start + (clockwise ? -visibleMagnitude : visibleMagnitude) * step);
                    var cutNormal = GetSegmentNormal(halfSize, cutAngle) * (clockwise ? 1f : -1f);
                    DrawClippedSegment(vh, vertices, samples, in colors, aaWidth, startAa, endAa,
                        GetSegmentNormal(halfSize, first), -GetSegmentNormal(halfSize, last), halfGap, cutNormal);
                    continue;
                }

                if (hasAa)
                {
                    SetSegmentBoundaryEdges(vertices, samples, startAa, endAa);
                    PrepareSegmentFringe(vertices, aaWidth, rect.center,
                        GetSegmentNormal(halfSize, first), -GetSegmentNormal(halfSize, last), halfGap);
                }

                DrawSegment(vh, vertices, samples, in colors, hasAa);
            }

            if (vh.currentVertCount == 0 && halfGap > 0f && !hasPartialSlot)
            {
                MeshDiagnostic = "The gap leaves no visible slices at this size. Reduce Gap or Segment Count.";
            }
        }

        private static bool FillSegmentVertices(ref SpanBuffer<SegmentVert> buffer, Vector2 center, Vector2 halfSize,
            float innerRadius, float first, float last, int samples, float halfGap)
        {
            var firstNormal = GetSegmentNormal(halfSize, first);
            var lastNormal = -GetSegmentNormal(halfSize, last);
            // In unit-ellipse coordinates, moving the cutting line inward by half of the
            // visible gap shifts the boundary parameter by the inverse sine of that offset.
            var firstShift = halfGap / Vector2.Scale(firstNormal, halfSize).magnitude;
            var lastShift = halfGap / Vector2.Scale(lastNormal, halfSize).magnitude;
            var outerStart = first + Mathf.Asin(Mathf.Clamp01(firstShift));
            var outerEnd = last - Mathf.Asin(Mathf.Clamp01(lastShift));
            if (firstShift >= 1f || lastShift >= 1f || outerEnd <= outerStart)
            {
                return false;
            }

            for (int i = 0; i < samples; i++)
            {
                var angle = Mathf.Lerp(outerStart, outerEnd, i / (float)(samples - 1));
                buffer.Add(new SegmentVert { Position = center + GetEllipsePoint(halfSize, angle) });
            }

            var innerStart = first + Mathf.Asin(Mathf.Clamp01(firstShift / Mathf.Max(float.Epsilon, innerRadius)));
            var innerEnd = last - Mathf.Asin(Mathf.Clamp01(lastShift / Mathf.Max(float.Epsilon, innerRadius)));
            if (innerRadius > 0f && firstShift < innerRadius && lastShift < innerRadius && innerEnd > innerStart)
            {
                // Reverse the inner arc so the complete slice boundary is counterclockwise.
                var innerSize = halfSize * innerRadius;
                for (int i = 0; i < samples; i++)
                {
                    var angle = Mathf.Lerp(innerEnd, innerStart, i / (float)(samples - 1));
                    buffer.Add(new SegmentVert { Position = center + GetEllipsePoint(innerSize, angle) });
                }
            }
            else
            {
                // Both sides can meet outside the inner ellipse on a very narrow slice.
                var combinedNormal = firstNormal + lastNormal;
                var divisor = combinedNormal.sqrMagnitude * 0.5f;
                var inset = halfGap > 0f && divisor > 0f ? combinedNormal * (halfGap / divisor) : Vector2.zero;
                buffer.Add(new SegmentVert { Position = center + inset });
            }

            return true;
        }

        private static Vector2 GetSegmentNormal(Vector2 halfSize, float angle)
        {
            var ray = GetEllipsePoint(halfSize, angle);
            return Vector2.Perpendicular(ray / Mathf.Max(float.Epsilon, ray.magnitude));
        }

        private static Vector2 GetEllipsePoint(Vector2 halfSize, float angle)
        {
            return new Vector2(halfSize.x * Mathf.Cos(angle), halfSize.y * Mathf.Sin(angle));
        }

        private static void SetSegmentBoundaryEdges(Span<SegmentVert> vertices, int samples, bool startAa, bool endAa)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                var previous = (i + vertices.Length - 1) % vertices.Length;
                vertices[i].AaOnIncomingEdge = HasSegmentFringe(previous, samples, vertices.Length, startAa, endAa);
            }
        }

        private static void PrepareSegmentFringe(Span<SegmentVert> vertices, float width,
            Vector2 center, Vector2 startNormal, Vector2 endNormal, float halfGap)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                var previous = (i + vertices.Length - 1) % vertices.Length;
                var next = (i + 1) % vertices.Length;
                var hasPrevious = vertices[i].AaOnIncomingEdge;
                var hasNext = vertices[next].AaOnIncomingEdge;
                var position = vertices[i].Position;
                var previousNormal = hasPrevious ? Vector2.Perpendicular((vertices[previous].Position - position).normalized) : Vector2.zero;
                var nextNormal = hasNext ? Vector2.Perpendicular((position - vertices[next].Position).normalized) : Vector2.zero;
                var normal = (previousNormal + nextNormal).normalized;
                var miter = width / Mathf.Max(0.5f, Mathf.Max(Vector2.Dot(normal, previousNormal), Vector2.Dot(normal, nextNormal)));
                var aaPosition = position + normal * miter;
                if (halfGap > 0f)
                {
                    // Keep neighbouring fringes on their own side of the visible gap.
                    aaPosition = center + ConstrainSegmentFringe(aaPosition - center, startNormal, endNormal);
                }

                vertices[i].AaPosition = aaPosition;
            }
        }

        private static Vector2 ConstrainSegmentFringe(Vector2 position, Vector2 startNormal, Vector2 endNormal)
        {
            var fromStart = Vector2.Dot(startNormal, position);
            var fromEnd = Vector2.Dot(endNormal, position);
            if (fromStart >= 0f && fromEnd >= 0f)
            {
                return position;
            }

            if (fromStart < 0f)
            {
                var startProjection = position - startNormal * fromStart;
                if (Vector2.Dot(endNormal, startProjection) >= 0f)
                {
                    return startProjection;
                }
            }

            if (fromEnd < 0f)
            {
                var endProjection = position - endNormal * fromEnd;
                if (Vector2.Dot(startNormal, endProjection) >= 0f)
                {
                    return endProjection;
                }
            }

            return Vector2.zero;
        }

        private static bool HasSegmentFringe(int index, int samples, int count, bool startAa, bool endAa)
        {
            return (index != samples - 1 || endAa) && (index != count - 1 || startAa);
        }

        private void DrawSegment(VertexHelper vh, Span<SegmentVert> vertices, int samples,
            in ColorCalculationData colors, bool hasAa)
        {
            AddSegmentVertices(vh, vertices, in colors, hasAa);

            if (vertices.Length == samples + 1)
            {
                for (int i = 1; i < samples; i++)
                {
                    GraphicMesh.AddFanTriangle(vh, vertices[samples].Index, vertices[i - 1].Index, vertices[i].Index);
                }
            }
            else
            {
                for (int i = 1; i < samples; i++)
                {
                    var inner = vertices.Length - i - 1;
                    GraphicMesh.AddStrip(vh, vertices[inner + 1].Index, vertices[i - 1].Index, vertices[inner].Index, vertices[i].Index);
                }
            }

            if (!hasAa)
            {
                return;
            }

            DrawSegmentFringe(vh, vertices);
        }

        private void AddSegmentVertices(VertexHelper vh, Span<SegmentVert> vertices, in ColorCalculationData colors, bool hasAa)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                ref var vertex = ref vertices[i];
                var tint = CalculateColor(in vertex.Position, in colors);
                vertex.Index = AddVert(vh, vertex.Position, tint, colors.EnclosingRect);
                if (hasAa)
                {
                    vertex.AaIndex = AddVert(vh, vertex.AaPosition, tint.WithAlpha(0f), colors.EnclosingRect);
                }
            }
        }

        private static void DrawSegmentFringe(VertexHelper vh, ReadOnlySpan<SegmentVert> vertices)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                var next = (i + 1) % vertices.Length;
                if (vertices[next].AaOnIncomingEdge)
                {
                    GraphicMesh.AddStrip(vh, vertices[i].Index, vertices[i].AaIndex, vertices[next].Index, vertices[next].AaIndex);
                }
            }
        }

        private void DrawClippedSegment(VertexHelper vh, ReadOnlySpan<SegmentVert> vertices, int samples,
            in ColorCalculationData colors, float aaWidth, bool startAa, bool endAa,
            Vector2 startNormal, Vector2 endNormal, float halfGap, Vector2 cutNormal)
        {
            var center = colors.EnclosingRect.center;
            // Clip the final fixed cell's triangles without moving its sampled arcs,
            // corner cuts, or gap boundaries as the reveal angle advances.
            var isFan = vertices.Length == samples + 1;
            for (int i = 1; i < samples; i++)
            {
                var previousOuter = vertices[i - 1].Position;
                var outer = vertices[i].Position;
                if (isFan)
                {
                    DrawClippedTriangle(vh, vertices[samples].Position, outer, previousOuter, cutNormal, center, in colors);
                }
                else
                {
                    var inner = vertices.Length - i - 1;
                    DrawClippedTriangle(vh, vertices[inner + 1].Position, vertices[inner].Position, previousOuter, cutNormal, center, in colors);
                    DrawClippedTriangle(vh, vertices[inner].Position, outer, previousOuter, cutNormal, center, in colors);
                }
            }

            if (aaWidth <= 0f)
            {
                return;
            }

            using var storage = SpanBufferSharedPool.RentBuffer(vertices.Length * 2, out SpanBuffer<SegmentVert> fringe);
            var previous = vertices[^1].Position;
            var previousDistance = Vector2.Dot(cutNormal, previous - center);
            for (int i = 0; i < vertices.Length; i++)
            {
                var current = vertices[i].Position;
                var distance = Vector2.Dot(cutNormal, current - center);
                var edge = (i + vertices.Length - 1) % vertices.Length;
                var hasFringe = HasSegmentFringe(edge, samples, vertices.Length, startAa, endAa);
                if (CrossesSliceCut(previousDistance, distance))
                {
                    fringe.Add(new SegmentVert
                    {
                        Position = IntersectSliceCut(previous, current, previousDistance, distance),
                        AaOnIncomingEdge = previousDistance < 0f || hasFringe,
                    });
                }

                if (distance >= 0f)
                {
                    fringe.Add(new SegmentVert
                    {
                        Position = current,
                        AaOnIncomingEdge = previousDistance < 0f && distance == 0f || hasFringe,
                    });
                }

                previous = current;
                previousDistance = distance;
            }

            var outline = fringe.FilledBufferSlice();
            if (outline.Length >= 3)
            {
                PrepareSegmentFringe(outline, aaWidth, center, startNormal, endNormal, halfGap);
                AddSegmentVertices(vh, outline, in colors, true);
                DrawSegmentFringe(vh, outline);
            }
        }

        private void DrawClippedTriangle(VertexHelper vh, Vector2 first, Vector2 second, Vector2 third,
            Vector2 cutNormal, Vector2 center, in ColorCalculationData colors)
        {
            Span<Vector2> triangle = stackalloc Vector2[] { first, second, third };
            Span<Vector2> clipped = stackalloc Vector2[4];
            var count = 0;
            var previous = third;
            var previousDistance = Vector2.Dot(cutNormal, previous - center);
            foreach (var current in triangle)
            {
                var distance = Vector2.Dot(cutNormal, current - center);
                if (CrossesSliceCut(previousDistance, distance))
                {
                    clipped[count++] = IntersectSliceCut(previous, current, previousDistance, distance);
                }

                if (distance >= 0f)
                {
                    clipped[count++] = current;
                }

                previous = current;
                previousDistance = distance;
            }

            if (count < 3)
            {
                return;
            }

            var firstIndex = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                var position = clipped[i];
                AddVert(vh, position, CalculateColor(in position, in colors), colors.EnclosingRect);
            }

            vh.AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
            if (count == 4)
            {
                vh.AddTriangle(firstIndex, firstIndex + 2, firstIndex + 3);
            }
        }

        private static bool CrossesSliceCut(float firstDistance, float secondDistance)
        {
            return firstDistance < 0f && secondDistance > 0f || firstDistance > 0f && secondDistance < 0f;
        }

        private static Vector2 IntersectSliceCut(Vector2 first, Vector2 second, float firstDistance, float secondDistance)
        {
            return (second * firstDistance - first * secondDistance) / (firstDistance - secondDistance);
        }
        
        private void GenerateFill(VertexHelper vh, in EllipseInfo info, bool insideEdge = false)
        {
            var halfSize = insideEdge ? info.EllipseEdgeHalfSize : info.EllipseHalfSize;
            if (halfSize.x <= 0f || halfSize.y <= 0f)
            {
                return;
            }

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullVertCount, out SpanBuffer<Vert> buffer);
            FillHullVerts(in info, ref buffer);

            var center = info.EnclosingRect.center;
            var aaWidth = GetOuterAaWidth(in info);
            if (insideEdge)
            {
                aaWidth = Mathf.Min(aaWidth, info.EllipseHalfSize.x - halfSize.x);
            }

            var aaHalfSize = halfSize + Vector2.one * aaWidth;
            var filledVerts = buffer.FilledBufferSlice();
            if (insideEdge)
            {
                for (int i = 0; i < filledVerts.Length; i++)
                {
                    ref var vert = ref filledVerts[i];
                    vert.Position = vert.EdgePosition;
                    var radius = vert.Position - center;
                    vert.AaPosition = center + (info.IsClosed
                        ? Vector2.Scale(new Vector2(radius.x / halfSize.x, radius.y / halfSize.y), aaHalfSize)
                        : GetRadiusOnRay(radius, aaHalfSize));
                }
            }

            var hasAa = aaWidth > 0f;
            if (hasAa && !info.IsClosed)
            {
                JoinOuterFringe(ref filledVerts[0], center, aaHalfSize, aaWidth, -1);
                JoinOuterFringe(ref filledVerts[^1], center, aaHalfSize, aaWidth, 1);
            }

            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var centerColor = CalculateColor(in center, in colorData);
            var centerIndex = AddVert(vh, center, centerColor, in info.EnclosingRect);

            for (int i = 0; i < filledVerts.Length; i++)
            {
                ref var vert = ref filledVerts[i];
                var vertColor = CalculateColor(in vert.Position, in colorData);
                vert.Index = AddVert(vh, vert.Position, vertColor, in info.EnclosingRect);
                if (hasAa)
                {
                    vert.AaIndex = vh.currentVertCount;
                    vh.AddVert(vert.AaPosition, vertColor.WithAlpha(0), CalculateUv(in vert.Position, in info.EnclosingRect));
                }
            }

            for (int i = 0; i < info.HullSections; i++)
            {
                var prevVert = filledVerts[i];
                var vert = filledVerts[(i + 1) % filledVerts.Length];
                GraphicMesh.AddFanTriangle(vh, centerIndex, prevVert.Index, vert.Index);

                if (hasAa)
                {
                    DrawStrip(vh, prevVert.Index, prevVert.AaIndex, vert.Index, vert.AaIndex);
                }
            }

            if (!info.IsClosed && hasAa)
            {
                var first = filledVerts[0];
                var last = filledVerts[^1];
                AddSectorCenterFringe(vh, in info, first.Position, last.Position, aaWidth, centerIndex, centerColor,
                    out var firstCenterAa, out var lastCenterAa);
                DrawCapStrip(vh, centerIndex, firstCenterAa, first.Index, first.AaIndex, -1);
                DrawCapStrip(vh, centerIndex, lastCenterAa, last.Index, last.AaIndex, 1);
            }
        }

        private void GenerateEdge(VertexHelper vh, in EllipseInfo info, bool shouldFill, in DashPattern pattern)
        {
            var mainColorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var edgeColorData = new ColorCalculationData(info.EnclosingRect, _edgeColor, _edgeColorGradient, _edgeGradientType, _shouldUseGradientForEdge);

            // Keep the gradient's intermediate colors and alpha in the mesh.
            var isRadial = _shouldUseGradientForEdge && _edgeGradientType == GradientType.Radial;
            var colorKeys = isRadial ? _edgeColorGradient.colorKeys : null;
            var alphaKeys = isRadial ? _edgeColorGradient.alphaKeys : null;
            Span<float> stops = stackalloc float[2 + (colorKeys?.Length ?? 0) + (alphaKeys?.Length ?? 0)];
            stops[0] = 0;
            stops[1] = 1;
            var stopCount = 2;
            if (isRadial)
            {
                foreach (var key in colorKeys)
                {
                    AddGradientStop(stops, ref stopCount, key.time);
                }

                foreach (var key in alphaKeys)
                {
                    AddGradientStop(stops, ref stopCount, key.time);
                }
            }

            stops = stops[..stopCount];
            Span<Color32> radialColors = stackalloc Color32[isRadial ? stopCount : 0];
            for (int i = 0; i < radialColors.Length; i++)
            {
                radialColors[i] = _edgeColorGradient.Evaluate(stops[i]);
            }

            if (info.IsDashed && !pattern.IsContinuous)
            {
                GenerateDashedEdge(vh, in info, in pattern, shouldFill, stops, radialColors, in mainColorData, in edgeColorData);
                return;
            }

            GenerateEdgeSpan(vh, in info, shouldFill, stops, radialColors, in mainColorData, in edgeColorData);
        }

        private void GenerateEdgeSpan(VertexHelper vh, in EllipseInfo info, bool shouldFill,
            ReadOnlySpan<float> stops, ReadOnlySpan<Color32> radialColors,
            in ColorCalculationData mainColorData, in ColorCalculationData edgeColorData)
        {
            var center = info.EnclosingRect.center;
            var stopCount = stops.Length;
            var isRadial = radialColors.Length > 0;

            var centerIndex = -1;
            var centerColor = default(Color32);
            if (shouldFill)
            {
                centerColor = CalculateColor(in center, in mainColorData);
                centerIndex = AddVert(vh, center, centerColor, in info.EnclosingRect);
            }

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullVertCount, out SpanBuffer<Vert> buffer);
            FillHullVerts(in info, ref buffer);
            var filledVerts = buffer.FilledBufferSlice();
            if (!info.IsClosed && !_roundCaps && info.IsAaEnabled)
            {
                JoinFlatEdgeFringe(ref filledVerts[0], in info, shouldFill, -1);
                JoinFlatEdgeFringe(ref filledVerts[^1], in info, shouldFill, 1);
            }

            for (int i = 0; i < filledVerts.Length; i++)
            {
                ref var vert = ref filledVerts[i];
                vert.EdgeIndex = vh.currentVertCount;
                for (int j = 0; j < stopCount; j++)
                {
                    var position = Vector2.LerpUnclamped(vert.EdgePosition, vert.Position, stops[j]);
                    var edgeColor = isRadial ? radialColors[j] : CalculateColor(in position, in edgeColorData);
                    AddVert(vh, position, edgeColor, in info.EnclosingRect);
                }

                vert.Index = vh.currentVertCount - 1;
                var innerColor = shouldFill
                    ? CalculateColor(in vert.AaEdgePosition, in mainColorData)
                    : CalculateEdgeColor(in vert.AaEdgePosition, 0, in edgeColorData).WithAlpha(0);
                vert.AaEdgeIndex = AddVert(vh, vert.AaEdgePosition, innerColor, in info.EnclosingRect);

                if (info.IsAaEnabled)
                {
                    var outerColor = CalculateEdgeColor(in vert.AaPosition, 1, in edgeColorData).WithAlpha(0);
                    vert.AaIndex = AddVert(vh, vert.AaPosition, outerColor, in info.EnclosingRect);
                }

            }

            for (int i = 0; i < info.HullSections; i++)
            {
                var prevVert = filledVerts[i];
                var vert = filledVerts[(i + 1) % filledVerts.Length];
                if (shouldFill)
                {
                    GraphicMesh.AddFanTriangle(vh, centerIndex, prevVert.AaEdgeIndex, vert.AaEdgeIndex);
                }

                DrawStrip(vh, prevVert.AaEdgeIndex, prevVert.EdgeIndex, vert.AaEdgeIndex, vert.EdgeIndex);
                for (int j = 1; j < stopCount; j++)
                {
                    DrawStrip(vh, prevVert.EdgeIndex + j - 1, prevVert.EdgeIndex + j, vert.EdgeIndex + j - 1, vert.EdgeIndex + j);
                }

                if (info.IsAaEnabled)
                {
                    DrawStrip(vh, prevVert.Index, prevVert.AaIndex, vert.Index, vert.AaIndex);
                }

            }

            if (info.IsClosed)
            {
                return;
            }

            var first = filledVerts[0];
            var last = filledVerts[^1];
            var firstCenterAa = -1;
            var lastCenterAa = -1;
            if (shouldFill && info.IsAaEnabled)
            {
                AddSectorCenterFringe(vh, in info, first.Position, last.Position, GetOuterAaWidth(in info), centerIndex, centerColor,
                    out firstCenterAa, out lastCenterAa);
            }

            if (_roundCaps)
            {
                DrawRoundEdgeCap(vh, in first, -1, stops, in info, in edgeColorData);
                DrawRoundEdgeCap(vh, in last, 1, stops, in info, in edgeColorData);
                if (shouldFill && info.IsAaEnabled)
                {
                    DrawFilledEdgeSide(vh, in first, -1, in info, centerIndex, firstCenterAa);
                    DrawFilledEdgeSide(vh, in last, 1, in info, centerIndex, lastCenterAa);
                }
            }
            else if (info.IsAaEnabled)
            {
                DrawFlatEdgeCap(vh, in first, -1, in info, centerIndex, firstCenterAa);
                DrawFlatEdgeCap(vh, in last, 1, in info, centerIndex, lastCenterAa);
            }
        }

        private void GenerateDashedEdge(VertexHelper vh, in EllipseInfo info, in DashPattern pattern, bool shouldFill,
            ReadOnlySpan<float> stops, ReadOnlySpan<Color32> radialColors,
            in ColorCalculationData mainColorData, in ColorCalculationData edgeColorData)
        {
            var sweep = Mathf.Abs(ClampSweep(_sweepAngle));
            // The fill has a continuous inner contour and its own paint. Only dash
            // geometry uses edge paint, including the inner transition on each dash.
            if (shouldFill)
            {
                GenerateFill(vh, in info, true);
            }

            var edgeWidth = info.EllipseHalfSize.x - info.EllipseEdgeHalfSize.x;
            var outerAa = info.EllipseAaHalfSize.x - info.EllipseHalfSize.x;
            var innerAa = info.EllipseEdgeHalfSize.x - info.EllipseEdgeAaHalfSize.x;
            var circleCapInset = _roundCaps && info.EllipseHalfSize.x == info.EllipseHalfSize.y
                ? Mathf.Asin(Mathf.Clamp01(edgeWidth / (2 * info.EllipseHalfSize.x - edgeWidth)))
                : -1;
            var clockwise = info.IsClockwise;
            var startAngle = NormalizeStartAngle(_startAngle);
            var halfGap = pattern.Gap * 0.5f;
            for (int i = 0; i < pattern.Count; i++)
            {
                var from = i * pattern.Period + halfGap;
                var to = Mathf.Min((i + 1) * pattern.Period - halfGap, sweep - halfGap);
                if (to <= from)
                {
                    continue;
                }

                // Tessellate each visible interval in the same CCW winding. Pattern order
                // still follows Sweep Angle, including clockwise partial arcs.
                var first = (startAngle + (clockwise ? -to : from)) * Mathf.Deg2Rad;
                var last = (startAngle + (clockwise ? -from : to)) * Mathf.Deg2Rad;
                if (_roundCaps)
                {
                    if (circleCapInset >= 0)
                    {
                        first += circleCapInset;
                        last -= circleCapInset;
                    }
                    else
                    {
                        var visibleFirst = first;
                        first = MoveCapInside(first, last, 1, in info);
                        last = MoveCapInside(last, visibleFirst, -1, in info);
                    }
                }
                if (last - first <= 0.000001f)
                {
                    continue;
                }

                var span = new EllipseInfo(info.EnclosingRect, info.FullHullSections,
                    first * Mathf.Rad2Deg, (last - first) * Mathf.Rad2Deg, false, outerAa, edgeWidth, innerAa);
                GenerateEdgeSpan(vh, in span, false, stops, radialColors, in mainColorData, in edgeColorData);
            }
        }

        private static float MoveCapInside(float boundary, float otherBoundary, int direction, in EllipseInfo info)
        {
            var low = 0f;
            var high = Mathf.Abs(otherBoundary - boundary);
            // The cap radius changes around an ellipse. Solve at the actual endpoint,
            // rather than using an average radius or coupling the gap to mesh sections.
            for (int i = 0; i < 22; i++)
            {
                var distance = (low + high) * 0.5f;
                var seam = boundary + distance * direction;
                var capBoundary = GetCapBoundary(seam, -direction, in info);
                if ((capBoundary - boundary) * direction < 0) low = distance;
                else high = distance;
            }
            return boundary + high * direction;
        }

        private static float GetCapBoundary(float angle, int direction, in EllipseInfo info)
        {
            var ray = Vector2.Scale(info.EllipseHalfSize, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            var inner = GetRadiusOnRay(ray, info.EllipseEdgeHalfSize);
            var center = (ray + inner) * 0.5f;
            var radius = Vector2.Distance(ray, inner) * 0.5f;
            var distance = center.magnitude;
            if (distance <= 0) return angle;

            // Tangent from the ellipse center to the semicircular cap. Convert the
            // resulting polar ray back to ellipse-parameter degrees used by Start/Sweep.
            var polar = Mathf.Atan2(center.y, center.x) + direction * Mathf.Asin(Mathf.Clamp01(radius / distance));
            var parameter = Mathf.Atan2(info.EllipseHalfSize.x * Mathf.Sin(polar),
                info.EllipseHalfSize.y * Mathf.Cos(polar));
            return angle + Mathf.DeltaAngle(angle * Mathf.Rad2Deg, parameter * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        }

        private static Vert CreateHullVert(int index, in EllipseInfo info)
        {
            var angle = info.StartAngleRadians + index * info.AngleDeltaRadians;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var center = info.EnclosingRect.center;
            var radius = Vector2.Scale(info.EllipseHalfSize, direction);
            var aaRadius = Vector2.Scale(info.EllipseAaHalfSize, direction);
            var edgeRadius = Vector2.Scale(info.EllipseEdgeHalfSize, direction);
            var aaEdgeRadius = Vector2.Scale(info.EllipseEdgeAaHalfSize, direction);
            if (!info.IsClosed)
            {
                // The same ellipse parameter on different layers does not lie on one ray.
                aaRadius = GetRadiusOnRay(radius, info.EllipseAaHalfSize);
                edgeRadius = GetRadiusOnRay(radius, info.EllipseEdgeHalfSize);
                aaEdgeRadius = GetRadiusOnRay(radius, info.EllipseEdgeAaHalfSize);
            }

            return new Vert
            {
                Position = center + radius,
                AaPosition = center + aaRadius,
                EdgePosition = center + edgeRadius,
                AaEdgePosition = center + aaEdgeRadius,
            };
        }

        private static void FillHullVerts(in EllipseInfo info, ref SpanBuffer<Vert> buffer)
        {
            for (int i = 0; i < info.HullVertCount; i++)
            {
                buffer.Add(CreateHullVert(i, in info));
            }
        }

        private static Vector2 GetRadiusOnRay(in Vector2 radius, in Vector2 halfSize)
        {
            if (halfSize.x <= 0 || halfSize.y <= 0)
            {
                return Vector2.zero;
            }

            var scaled = new Vector2(radius.x / halfSize.x, radius.y / halfSize.y);
            return radius / scaled.magnitude;
        }

        private void DrawRoundEdgeCap(VertexHelper vh, in Vert vert, int direction, ReadOnlySpan<float> stops, in EllipseInfo info, in ColorCalculationData colorData)
        {
            var edge = vert.Position - vert.EdgePosition;
            var thickness = edge.magnitude;
            if (thickness <= 0)
            {
                return;
            }

            var innerAa = Vector2.Distance(vert.EdgePosition, vert.AaEdgePosition);
            var outerAa = info.IsAaEnabled ? Vector2.Distance(vert.Position, vert.AaPosition) : 0f;
            var hasAa = innerAa > 0 || info.IsAaEnabled;

            // Sample the same semicircle as a round line cap, adding the gradient stops
            // to its diameter so the colors agree with the adjoining edge.
            Span<float> samples = stackalloc float[stops.Length + RoundCapSections - 1];
            stops.CopyTo(samples);
            var sampleCount = stops.Length;
            for (int i = 1; i < RoundCapSections; i++)
            {
                var t = (1 - Mathf.Cos(i * Mathf.PI / RoundCapSections)) * 0.5f;
                AddGradientStop(samples, ref sampleCount, t);
            }

            var across = edge / thickness;
            var along = Vector2.Perpendicular(across) * direction;
            var prevSeam = -1;
            var prevCap = -1;
            var prevAa = -1;
            for (int i = 0; i < sampleCount; i++)
            {
                var t = samples[i];
                var seam = vert.EdgePosition + edge * t;
                var rise = Mathf.Sqrt(t * (1 - t));
                var position = seam + along * (thickness * rise);
                var seamColor = CalculateEdgeColor(in seam, t, in colorData);
                var seamIndex = AddVert(vh, seam, seamColor, in info.EnclosingRect);
                var capIndex = rise > 0
                    ? AddVert(vh, position, CalculateEdgeColor(in position, t, in colorData), in info.EnclosingRect)
                    : seamIndex;
                var aaIndex = capIndex;
                if (hasAa)
                {
                    var aaDirection = across * (2 * t - 1) + along * (2 * rise);
                    var aaPosition = position + aaDirection * Mathf.LerpUnclamped(innerAa, outerAa, t);
                    aaIndex = AddVert(vh, aaPosition, seamColor.WithAlpha(0), in info.EnclosingRect);
                }

                if (i > 0)
                {
                    DrawCapStrip(vh, prevSeam, prevCap, seamIndex, capIndex, direction);
                    if (hasAa)
                    {
                        DrawCapStrip(vh, prevCap, prevAa, capIndex, aaIndex, direction);
                    }
                }

                prevSeam = seamIndex;
                prevCap = capIndex;
                prevAa = aaIndex;
            }
        }

        private static void JoinFlatEdgeFringe(ref Vert vert, in EllipseInfo info, bool shouldFill, int direction)
        {
            var center = info.EnclosingRect.center;
            var aaWidth = GetOuterAaWidth(in info);
            var radial = (vert.Position - center).normalized;
            var sideOffset = Vector2.Perpendicular(radial) * (aaWidth * direction);
            vert.AaPosition = JoinCutAndArc(vert.Position, vert.AaPosition, center, info.EllipseAaHalfSize, radial, sideOffset);
            if (!shouldFill && vert.AaEdgePosition != vert.EdgePosition)
            {
                vert.AaEdgePosition = JoinCutAndArc(vert.EdgePosition, vert.AaEdgePosition, center,
                    info.EllipseEdgeAaHalfSize, radial, sideOffset);
            }
        }

        private static void JoinOuterFringe(ref Vert vert, Vector2 center, Vector2 halfSize, float aaWidth, int direction)
        {
            var radial = (vert.Position - center).normalized;
            var sideOffset = Vector2.Perpendicular(radial) * (aaWidth * direction);
            vert.AaPosition = JoinCutAndArc(vert.Position, vert.AaPosition, center, halfSize, radial, sideOffset);
        }

        private static Vector2 JoinCutAndArc(Vector2 corner, Vector2 fringe, Vector2 center, Vector2 halfSize,
            Vector2 radial, Vector2 sideOffset)
        {
            // The cap and the arc reference this same transparent corner. Its position
            // lies where the shifted cut meets the tangent of the AA ellipse.
            var origin = corner + sideOffset;
            var distance = Vector2.Dot(fringe - origin, radial);
            if (halfSize.x > 0f && halfSize.y > 0f)
            {
                var delta = fringe - center;
                var normal = new Vector2(delta.x / halfSize.x / halfSize.x, delta.y / halfSize.y / halfSize.y);
                var denominator = Vector2.Dot(radial, normal);
                if (denominator > 0f)
                {
                    distance = Vector2.Dot(fringe - origin, normal) / denominator;
                }
            }

            var limit = Mathf.Max((fringe - corner).magnitude, sideOffset.magnitude) * MaximumAaMiter;
            return origin + radial * Mathf.Clamp(distance, -limit, limit);
        }

        private static void DrawFlatEdgeCap(VertexHelper vh, in Vert vert, int direction, in EllipseInfo info,
            int centerIndex, int centerAaIndex)
        {
            var offset = GetSideAaOffset(vert.Position, direction, GetOuterAaWidth(in info), in info);
            var hasFill = centerIndex >= 0;
            var previousIndex = centerIndex;
            var previousAaIndex = centerAaIndex;
            if (hasFill && vert.AaEdgePosition != vert.EdgePosition)
            {
                var innerAaIndex = AddSideFringe(vh, vert.AaEdgeIndex, offset);
                DrawCapStrip(vh, previousIndex, previousAaIndex, vert.AaEdgeIndex, innerAaIndex, direction);
                previousIndex = vert.AaEdgeIndex;
                previousAaIndex = innerAaIndex;
            }

            for (int index = vert.EdgeIndex; index <= vert.Index; index++)
            {
                int aaIndex;
                if (index == vert.Index)
                {
                    aaIndex = vert.AaIndex;
                }
                else if (index == vert.EdgeIndex && !hasFill && vert.AaEdgePosition != vert.EdgePosition)
                {
                    aaIndex = vert.AaEdgeIndex;
                }
                else
                {
                    aaIndex = AddSideFringe(vh, index, offset);
                }

                if (previousIndex >= 0)
                {
                    DrawCapStrip(vh, previousIndex, previousAaIndex, index, aaIndex, direction);
                }

                previousIndex = index;
                previousAaIndex = aaIndex;
            }
        }

        private static void DrawFilledEdgeSide(VertexHelper vh, in Vert vert, int direction, in EllipseInfo info,
            int centerIndex, int centerAaIndex)
        {
            var offset = GetSideAaOffset(vert.Position, direction, GetOuterAaWidth(in info), in info);
            var innerAa = AddSideFringe(vh, vert.AaEdgeIndex, offset);
            DrawCapStrip(vh, centerIndex, centerAaIndex, vert.AaEdgeIndex, innerAa, direction);
            if (vert.AaEdgePosition != vert.EdgePosition)
            {
                var outerAa = AddSideFringe(vh, vert.EdgeIndex, offset);
                DrawCapStrip(vh, vert.AaEdgeIndex, innerAa, vert.EdgeIndex, outerAa, direction);
            }
        }

        private static int AddSideFringe(VertexHelper vh, int index, Vector2 offset)
        {
            var vertex = UIVertex.simpleVert;
            vh.PopulateUIVertex(ref vertex, index);
            vertex.position += (Vector3)offset;
            vertex.color.a = 0;
            var fringeIndex = vh.currentVertCount;
            vh.AddVert(vertex);
            return fringeIndex;
        }

        private void AddSectorCenterFringe(VertexHelper vh, in EllipseInfo info, Vector2 first, Vector2 last,
            float width, int centerIndex, Color32 centerColor, out int firstAa, out int lastAa)
        {
            var center = info.EnclosingRect.center;
            var firstNormal = GetSideAaOffset(first, -1, 1f, in info);
            var lastNormal = GetSideAaOffset(last, 1, 1f, in info);
            var joinedNormal = firstNormal + lastNormal;
            var divisor = joinedNormal.sqrMagnitude * 0.5f;
            var minimumDivisor = 2f / (MaximumAaMiter * MaximumAaMiter);
            var transparent = centerColor.WithAlpha(0f);
            if (divisor >= minimumDivisor || info.AngleDeltaRadians * info.HullSections > Mathf.PI)
            {
                // A shared inner join keeps both sides of a reflex cut from drawing
                // over one another. Keep the join within both sides on small sectors.
                var miter = joinedNormal * (width / Mathf.Max(divisor, minimumDivisor));
                var reach = Mathf.Min((first - center).magnitude, (last - center).magnitude);
                var position = center + Vector2.ClampMagnitude(miter, reach);
                firstAa = AddVert(vh, position, transparent, in info.EnclosingRect);
                lastAa = firstAa;
                return;
            }

            firstAa = AddVert(vh, center + firstNormal * width, transparent, in info.EnclosingRect);
            lastAa = AddVert(vh, center + lastNormal * width, transparent, in info.EnclosingRect);
            vh.AddTriangle(centerIndex, firstAa, lastAa);
        }

        private static float GetOuterAaWidth(in EllipseInfo info)
        {
            return info.EllipseAaHalfSize.x - info.EllipseHalfSize.x;
        }

        private static Vector2 GetSideAaOffset(Vector2 position, int direction, float width, in EllipseInfo info)
        {
            return Vector2.Perpendicular((position - info.EnclosingRect.center).normalized) * (direction * width);
        }

        private static void DrawCapStrip(VertexHelper vh, int prevInner, int prevOuter, int inner, int outer, int direction)
        {
            if (direction > 0)
            {
                DrawStrip(vh, inner, outer, prevInner, prevOuter);
            }
            else
            {
                DrawStrip(vh, prevInner, prevOuter, inner, outer);
            }
        }

        private static void DrawStrip(VertexHelper vh, int prevInner, int prevOuter, int inner, int outer)
        {
            GraphicMesh.AddStrip(vh, prevInner, prevOuter, inner, outer);
        }

        private static void AddGradientStop(Span<float> stops, ref int count, float time)
        {
            var index = 0;
            while (index < count && stops[index] < time)
            {
                index++;
            }

            if (index < count && Mathf.Approximately(stops[index], time))
            {
                return;
            }

            stops[index..count].CopyTo(stops[(index + 1)..]);
            stops[index] = time;
            count++;
        }

        private int AddVert(VertexHelper vh, in Vector2 position, Color32 color, in Rect rect)
        {
            var index = vh.currentVertCount;
            vh.AddVert(position, color, CalculateUv(in position, in rect));
            return index;
        }

        private static Color32 CalculateEdgeColor(in Vector2 position, float acrossEdge, in ColorCalculationData colorData)
        {
            return colorData.ShouldUseGradient && colorData.GradientType == GradientType.Radial
                ? (Color32)colorData.GradientColor.Evaluate(acrossEdge)
                : CalculateColor(in position, in colorData);
        }

        private static Color32 CalculateColor(in Vector2 vertexPosition, in ColorCalculationData colorData)
        {
            if (!colorData.ShouldUseGradient)
            {
                return colorData.SingleColor;
            }

            var rect = colorData.EnclosingRect;
            
            switch (colorData.GradientType)
            {
                case GradientType.Horizontal:
                {
                    var t = (vertexPosition.x - rect.xMin) / rect.width;
                    return colorData.GradientColor.Evaluate(t);
                }
                case GradientType.Vertical:
                {
                    var t = (vertexPosition.y - rect.yMin) / rect.height;
                    return colorData.GradientColor.Evaluate(t);
                }
                case GradientType.Radial:
                {
                    var offset = vertexPosition - rect.center;
                    var normalized = new Vector2(2 * offset.x / rect.width, 2 * offset.y / rect.height);
                    return colorData.GradientColor.Evaluate(normalized.magnitude);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(colorData.GradientType), colorData.GradientType, "Unknown gradient type used.");
            }
        }

        private Vector4 CalculateUv(in Vector2 vertexPosition, in Rect enclosingRect)
        {
            return GraphicMesh.MapUv(vertexPosition, enclosingRect, _spriteUv);
        }
    }
}
