using System;
using JetBrains.Annotations;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Ellipse Graphic", 12)]
    public class EllipseGraphic : Image
    {
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        private const int RoundCapSections = 10;
        
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
            public readonly bool IsAaEnabled;
            public readonly bool IsDashed;
            public readonly bool IsClosed;
            public readonly bool IsClockwise;
            
            public EllipseInfo(Rect enclosingRect, int hullSections, float startAngle, float sweepAngle, bool isDashed, float aaWidth, float edgeWidth, float innerAaWidth)
            {
                EnclosingRect = enclosingRect;
                IsClosed = Mathf.Abs(sweepAngle) == 360;
                IsClockwise = sweepAngle < 0;
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

        internal enum Mode
        {
            Fill = 0,
            Edge = 2,
            EdgeAndFill = 3,
        };

        internal enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        };

        [FormerlySerializedAs("startAngle")]
        [SerializeField, Range(0.0f, 360.0f)]
        private int _startAngle = 0;

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
        private Gradient _gradientColor = new ();
        
        [FormerlySerializedAs("edgeColor")]
        [SerializeField]
        private Color _edgeColor;
        
        [SerializeField]
        private bool _shouldUseGradientForEdge;

        [SerializeField]
        private Gradient _edgeColorGradient = new Gradient();

        [SerializeField]
        private GradientType _edgeGradientType;
        
        [SerializeField, UsedImplicitly]
        private int _vertCount;
        
        [SerializeField, UsedImplicitly]
        private int _trianglesCount;
        
        [SerializeField]
        private bool _overrideStencil;
        
        [SerializeField]
        private CompareFunction _stencilComp;
    
        [SerializeField]
        private int _stencilRef;
        
        private (Material material, bool isSet) _materialContainer;

        public override Material materialForRendering
        {
            get
            {
                if (!_overrideStencil)
                {
                    return base.materialForRendering;
                }
                
                if (!_materialContainer.isSet)
                {
                    var materialCopy = new Material(base.materialForRendering);
                    materialCopy.SetInt(Stencil, _stencilRef);
                    materialCopy.SetInt(StencilComp, (int)_stencilComp);
                    
                    _materialContainer = (materialCopy, true);
                }
                
                return _materialContainer.material;
            }
        }
        
        private void ApplyStencil()
        {
            if (!_overrideStencil || !_materialContainer.isSet)
            {
                return;
            }
            
            var mat = _materialContainer.material;
            
            mat.SetInt(Stencil, _stencilRef);
            mat.SetInt(StencilComp, (int)_stencilComp);
        }
        
        protected override void Start()
        {
            base.Start();
            
            ApplyStencil();
        }
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyStencil();
        }
#endif
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

        public float SweepAngle
        {
            get => _sweepAngle;
            set
            {
                var sweepAngle = Mathf.Clamp(value, -360, 360);
                if (_sweepAngle == sweepAngle)
                {
                    return;
                }

                _sweepAngle = sweepAngle;
                SetVerticesDirty();
            }
        }
        
        public void SetColors(Color newMainColor, Color newEdgeColor)
        {
            color = newMainColor;
            _edgeColor = newEdgeColor;
            
            SetVerticesDirty();
        }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            _vertCount = 0;
            _trianglesCount = 0;
            vh.Clear();
            
            Rect enclosingRect = GetPixelAdjustedRect();
            
            var smallerSide = Math.Min(enclosingRect.width, enclosingRect.height);
            var sweepAngle = Mathf.Clamp(_sweepAngle, -360, 360);
            if (smallerSide <= 0 || sweepAngle == 0)
            {
                return;
            }

            var halfSmallerSide = smallerSide * 0.5f;
            if (_keepCircle)
            {
                enclosingRect = new Rect(enclosingRect.center - new Vector2(halfSmallerSide, halfSmallerSide), new Vector2(smallerSide, smallerSide));
            }

            var edge = _edgeThicknessMode switch
            {
                EdgeThicknessMode.Absolute => _edgeThickness,
                EdgeThicknessMode.Relative => _edgeThicknessRelative * halfSmallerSide,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(EdgeThicknessMode)}: {_edgeThicknessMode}"),
            };

            edge = Mathf.Clamp(edge, 0, halfSmallerSide);
            var ellipseInfo = new EllipseInfo(
                enclosingRect,
                Mathf.Max(3, _hullSections),
                _startAngle,
                sweepAngle,
                _isDashed,
                _antiAliasingLayerWidth,
                edge,
                Mathf.Clamp(_innerAntiAliasingLayerWidth, 0, halfSmallerSide - edge));
            
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
                    GenerateEdge(vh, in ellipseInfo, _mode == Mode.EdgeAndFill);
                    break;
                }
            }
            
            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }
        
        private void GenerateFill(VertexHelper vh, in EllipseInfo info)
        {
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullVertCount, out SpanBuffer<Vert> buffer);

            var center = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            AddVert(vh, center, CalculateColor(in center, in colorData), in info.EnclosingRect);

            for (int i = 0; i < info.HullVertCount; i++)
            {
                var vert = CreateHullVert(i, in info);
                var vertColor = CalculateColor(in vert.Position, in colorData);
                vert.Index = AddVert(vh, vert.Position, vertColor, in info.EnclosingRect);
                if (info.IsAaEnabled)
                {
                    vert.AaIndex = vh.currentVertCount;
                    vh.AddVert(vert.AaPosition, vertColor.WithAlpha(0), CalculateUv(in vert.Position, in info.EnclosingRect));
                }

                buffer.Add(vert);
            }

            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < info.HullSections; i++)
            {
                var prevVert = filledVerts[i];
                var vert = filledVerts[(i + 1) % filledVerts.Length];
                vh.AddTriangle(0, vert.Index, prevVert.Index);

                if (info.IsAaEnabled)
                {
                    DrawStrip(vh, prevVert.Index, prevVert.AaIndex, vert.Index, vert.AaIndex);
                }
            }

            if (!info.IsClosed && info.IsAaEnabled)
            {
                DrawFillSide(vh, filledVerts[0].Index, filledVerts[0].Position, filledVerts[0].AaIndex, -1, in info, in colorData);
                DrawFillSide(vh, filledVerts[^1].Index, filledVerts[^1].Position, filledVerts[^1].AaIndex, 1, in info, in colorData);
            }
        }

        private void GenerateEdge(VertexHelper vh, in EllipseInfo info, bool shouldFill)
        {
            var center = info.EnclosingRect.center;
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

            if (shouldFill)
            {
                AddVert(vh, center, CalculateColor(in center, in mainColorData), in info.EnclosingRect);
            }

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullVertCount, out SpanBuffer<Vert> buffer);
            for (int i = 0; i < info.HullVertCount; i++)
            {
                var vert = CreateHullVert(i, in info);
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

                buffer.Add(vert);
            }

            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < info.HullSections; i++)
            {
                var prevVert = filledVerts[i];
                var vert = filledVerts[(i + 1) % filledVerts.Length];
                var shouldDrawEdge = ShouldDrawEdge(i, in info);
                if (shouldFill)
                {
                    vh.AddTriangle(0, vert.AaEdgeIndex, prevVert.AaEdgeIndex);
                }

                if (shouldFill || shouldDrawEdge)
                {
                    DrawStrip(vh, prevVert.AaEdgeIndex, prevVert.EdgeIndex, vert.AaEdgeIndex, vert.EdgeIndex);
                }

                if (!shouldDrawEdge)
                {
                    continue;
                }

                for (int j = 1; j < stopCount; j++)
                {
                    DrawStrip(vh, prevVert.EdgeIndex + j - 1, prevVert.EdgeIndex + j, vert.EdgeIndex + j - 1, vert.EdgeIndex + j);
                }

                if (info.IsAaEnabled)
                {
                    DrawStrip(vh, prevVert.Index, prevVert.AaIndex, vert.Index, vert.AaIndex);
                }

                if (!ShouldDrawEdge(i - 1, in info))
                {
                    DrawEdgeCap(vh, in prevVert, -1, stops, in info, in edgeColorData, shouldFill, !info.IsClosed && i == 0);
                }

                if (!ShouldDrawEdge(i + 1, in info))
                {
                    DrawEdgeCap(vh, in vert, 1, stops, in info, in edgeColorData, shouldFill, !info.IsClosed && i == info.HullSections - 1);
                }
            }

            if (shouldFill && !info.IsClosed && info.IsAaEnabled)
            {
                if (_roundCaps || !ShouldDrawEdge(0, in info))
                {
                    DrawFilledEdgeSide(vh, in filledVerts[0], -1, in info, in mainColorData, in edgeColorData);
                }

                if (_roundCaps || !ShouldDrawEdge(info.HullSections - 1, in info))
                {
                    DrawFilledEdgeSide(vh, in filledVerts[^1], 1, in info, in mainColorData, in edgeColorData);
                }
            }
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

        private static Vector2 GetRadiusOnRay(in Vector2 radius, in Vector2 halfSize)
        {
            if (halfSize.x <= 0 || halfSize.y <= 0)
            {
                return Vector2.zero;
            }

            var scaled = new Vector2(radius.x / halfSize.x, radius.y / halfSize.y);
            return radius / scaled.magnitude;
        }

        private static bool ShouldDrawEdge(int index, in EllipseInfo info)
        {
            if (!info.IsClosed && (index < 0 || index >= info.HullSections))
            {
                return false;
            }

            if (!info.IsDashed)
            {
                return true;
            }

            index = (index + info.HullSections) % info.HullSections;
            if (info.IsClockwise)
            {
                index = info.HullSections - 1 - index;
            }

            // Match the original alternating pattern at the seam of a closed ellipse.
            return (info.IsClosed ? (index + 1) % info.HullSections : index) % 2 == 0;
        }

        private void DrawEdgeCap(VertexHelper vh, in Vert vert, int direction, ReadOnlySpan<float> stops, in EllipseInfo info, in ColorCalculationData colorData, bool shouldFill, bool isSectorEnd)
        {
            var edge = vert.Position - vert.EdgePosition;
            var thickness = edge.magnitude;
            if (!_roundCaps)
            {
                if (info.IsAaEnabled)
                {
                    DrawFlatEdgeCap(vh, in vert, direction, in info, shouldFill, isSectorEnd);
                }

                return;
            }

            if (thickness <= 0)
            {
                return;
            }

            var innerAa = Vector2.Distance(vert.EdgePosition, vert.AaEdgePosition);
            var outerAa = Vector2.Distance(vert.Position, vert.AaPosition);
            var hasAa = innerAa > 0 || outerAa > 0;

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

        private static void DrawFlatEdgeCap(VertexHelper vh, in Vert vert, int direction, in EllipseInfo info, bool shouldFill, bool isSectorEnd)
        {
            var shouldDrawFillSide = shouldFill && isSectorEnd;
            Span<int> side = stackalloc int[vert.Index - vert.EdgeIndex + 3];
            var count = 0;
            if (shouldDrawFillSide)
            {
                side[count++] = 0;
                side[count++] = vert.AaEdgeIndex;
            }

            for (int index = vert.EdgeIndex; index <= vert.Index; index++)
            {
                side[count++] = index;
            }

            var axis = isSectorEnd ? vert.Position - info.EnclosingRect.center : vert.Position - vert.EdgePosition;
            var aaWidth = info.EllipseAaHalfSize.x - info.EllipseHalfSize.x;
            var offset = Vector2.Perpendicular(axis.normalized) * (direction * aaWidth);
            var firstAa = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                var vertex = UIVertex.simpleVert;
                vh.PopulateUIVertex(ref vertex, side[i]);
                vertex.position += (Vector3)offset;
                vertex.color.a = 0;
                vh.AddVert(vertex);
                if (i > 0)
                {
                    DrawCapStrip(vh, side[i - 1], firstAa + i - 1, side[i], firstAa + i, direction);
                }
            }

            // Join both fringes at the same corner without extending a square past the cut.
            if (!shouldFill)
            {
                AddCapCorner(vh, vert.EdgeIndex, firstAa, vert.AaEdgeIndex, direction);
            }

            AddCapCorner(vh, vert.Index, vert.AaIndex, firstAa + count - 1, direction);
        }

        private static void AddCapCorner(VertexHelper vh, int index, int first, int second, int direction)
        {
            if (direction < 0)
            {
                vh.AddTriangle(index, first, second);
            }
            else
            {
                vh.AddTriangle(index, second, first);
            }
        }

        private static void DrawFilledEdgeSide(VertexHelper vh, in Vert vert, int direction, in EllipseInfo info, in ColorCalculationData mainColorData, in ColorCalculationData edgeColorData)
        {
            var innerAa = DrawFillSide(vh, vert.AaEdgeIndex, vert.AaEdgePosition, -1, direction, in info, in mainColorData);
            if (vert.AaEdgePosition != vert.EdgePosition)
            {
                var offset = GetSideAaOffset(vert.Position, direction, in info);
                var edgeColor = CalculateEdgeColor(in vert.EdgePosition, 0, in edgeColorData).WithAlpha(0);
                var outerAa = AddVert(vh, vert.EdgePosition + offset, edgeColor, in info.EnclosingRect);
                DrawCapStrip(vh, vert.AaEdgeIndex, innerAa, vert.EdgeIndex, outerAa, direction);
            }
        }

        private static int DrawFillSide(VertexHelper vh, int index, in Vector2 position, int arcAaIndex, int direction, in EllipseInfo info, in ColorCalculationData colorData)
        {
            var center = info.EnclosingRect.center;
            var offset = GetSideAaOffset(position, direction, in info);
            var centerAa = AddVert(vh, center + offset, CalculateColor(in center, in colorData).WithAlpha(0), in info.EnclosingRect);
            var outerAa = AddVert(vh, position + offset, CalculateColor(in position, in colorData).WithAlpha(0), in info.EnclosingRect);
            DrawCapStrip(vh, 0, centerAa, index, outerAa, direction);

            if (arcAaIndex >= 0)
            {
                if (direction < 0)
                {
                    vh.AddTriangle(index, arcAaIndex, outerAa);
                }
                else
                {
                    vh.AddTriangle(index, outerAa, arcAaIndex);
                }
            }

            return outerAa;
        }

        private static Vector2 GetSideAaOffset(in Vector2 position, int direction, in EllipseInfo info)
        {
            var aaWidth = info.EllipseAaHalfSize.x - info.EllipseHalfSize.x;
            return Vector2.Perpendicular((position - info.EnclosingRect.center).normalized) * (direction * aaWidth);
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
            if (prevInner != prevOuter)
            {
                vh.AddTriangle(prevInner, inner, prevOuter);
            }

            if (inner != outer)
            {
                vh.AddTriangle(inner, outer, prevOuter);
            }
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

        private static int AddVert(VertexHelper vh, in Vector2 position, Color32 color, in Rect rect)
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

        private static Vector4 CalculateUv(in Vector2 vertexPosition, in Rect enclosingRect)
        {
            var u = (vertexPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (vertexPosition.y - enclosingRect.yMin) / enclosingRect.height;
            
            return new Vector2(u, v);
        }
    }
}
