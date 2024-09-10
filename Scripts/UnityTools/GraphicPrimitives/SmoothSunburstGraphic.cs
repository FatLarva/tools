using System;
using JetBrains.Annotations;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/SmoothSunburstGraphic", 12)]
    public class SmoothSunburstGraphic : Image
    {
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilOp = Shader.PropertyToID("_StencilOp");
        private static readonly int ColorMask = Shader.PropertyToID("_ColorMask");
        
        private struct Vert
        {
            public Vector2 Position;
            public Vector2 AaPosition;
            public Vector2 EdgePosition;
            public Vector2 AaEdgePosition;
            public short Index;
            public short AaIndex;
            public short EdgeIndex;
            public short AaEdgeIndex;
        }

        private readonly ref struct HullSettings
        {
            public readonly Rect EnclosingRect;
            public readonly bool IsAaEnabled;
            public readonly Mode DrawingMode;

            public HullSettings(Rect enclosingRect, bool isAaEnabled, Mode drawingMode)
            {
                EnclosingRect = enclosingRect;
                IsAaEnabled = isAaEnabled;
                DrawingMode = drawingMode;
            }
        }

        private readonly ref struct ColorCalculationData
        {
            public readonly Rect EnclosingRect;
            public readonly Color SingleColor;
            public readonly Gradient GradientColor;
            public readonly GradientType GradientType;
            public readonly bool ShouldUseGradient;

            public ColorCalculationData(
                Rect enclosingRect,
                Color singleColor,
                Gradient gradientColor,
                GradientType gradientType,
                bool shouldUseGradient)
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
            public readonly Vector2 InnerHalfSize;
            public readonly Vector2 OuterHalfSize;
            public readonly float StartAngleRadians;
            public readonly float AngleDeltaRadians;
            public readonly float AaWidth;
            public readonly float EdgeWidth;
            public readonly float InnerAaWidth;
            public readonly int HullSections;
            public readonly bool IsAaEnabled;
            public readonly bool IsDashed;
            public readonly int CurvesSmoothness;
            public readonly float InnerTangentPointShift;
            public readonly float OuterTangentPointShift;
            public readonly float InnerTangentPointShiftPerp;
            public readonly float OuterTangentPointShiftPerp;
            public readonly float InnerOuterDiff;

            public EllipseInfo(
                Rect enclosingRect,
                int hullSections,
                float startAngleRadians,
                bool isDashed,
                float? aaWidth,
                float? edgeWidth,
                float? innerAaWidth,
                float innerDisplacementFactor,
                float outerDisplacementFactor,
                int curvesSmoothness,
                float innerTangentPointShift,
                float outerTangentPointShift,
                float innerTangentPointShiftPerp,
                float outerTangentPointShiftPerp)
            {
                EnclosingRect = enclosingRect;
                HullSections = hullSections;
                StartAngleRadians = startAngleRadians;
                AngleDeltaRadians = 2 * Mathf.PI / hullSections;
                IsAaEnabled = aaWidth > 0;
                IsDashed = isDashed;
                CurvesSmoothness = curvesSmoothness;
                InnerTangentPointShift = innerTangentPointShift;
                OuterTangentPointShift = outerTangentPointShift;
                InnerTangentPointShiftPerp = innerTangentPointShiftPerp;
                OuterTangentPointShiftPerp = outerTangentPointShiftPerp;

                AaWidth = aaWidth.GetValueOrDefault();
                EdgeWidth = edgeWidth.GetValueOrDefault();
                InnerAaWidth = EdgeWidth + innerAaWidth.GetValueOrDefault();

                InnerHalfSize = new Vector2(enclosingRect.width / 2, enclosingRect.height / 2) * innerDisplacementFactor;
                OuterHalfSize = new Vector2(enclosingRect.width / 2, enclosingRect.height / 2) * outerDisplacementFactor;
                
                InnerOuterDiff = Math.Abs(OuterHalfSize.magnitude - InnerHalfSize.magnitude);
            }
        }

        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }

        private enum Mode
        {
            FillInside = 0,
            Edge = 2,
            EdgeAndFill = 3,
        };

        private enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        };

        [SerializeField, Range(0.0f, 360.0f)]
        private int _startAngle = 0;

        [SerializeField, Min(3)]
        private int _peaksCount = 10;

        [SerializeField, Range(1f, 2f)]
        private float _outerDisplacementFactor = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _innerDisplacementFactor = 1f;

        [SerializeField]
        private bool _keepCircle = false;

        [SerializeField]
        private Mode _mode;

        [SerializeField]
        private EdgeThicknessMode _edgeThicknessMode;

        [SerializeField, Min(0)]
        [Tooltip("Edge mode only")]
        private float _edgeThickness = 1;

        [SerializeField, Range(0, 1)]
        [Tooltip("Edge mode only")]
        private float _edgeThicknessRelative = 0;

        [SerializeField]
        [Tooltip("Edge mode only")]
        private bool _isDashed = false;

        [SerializeField, Range(0, 100)]
        [Tooltip("When greater than 0 adds thin mesh edge around generated mesh with alpha transitioning from 1 to 0 so it'll look smoother.")]
        private float _antiAliasingLayerWidth = 0;

        [SerializeField, Range(0, 100)]
        [Tooltip("Thickness of the line where edge color interpolates into mainColor")]
        private float _innerAntiAliasingLayerWidth = 0;

        [SerializeField]
        private bool _shouldUseGradient;

        [SerializeField]
        private GradientType _gradientType;

        [SerializeField]
        private Gradient _gradientColor = new();

        [SerializeField]
        private Color _edgeColor;

        [SerializeField]
        private bool _shouldUseGradientForEdge;

        [SerializeField]
        private Gradient _edgeColorGradient = new Gradient();

        [SerializeField]
        private GradientType _edgeGradientType;

        [SerializeField, Range(-1, 1)]
        private float _innerTangentPointShift;

        [SerializeField, Range(-1, 1)]
        private float _outerTangentPointShift;
        
        [SerializeField, Range(-1, 1)]
        private float _innerTangentPointShiftPerp;

        [SerializeField, Range(-1, 1)]
        private float _outerTangentPointShiftPerp;

        [SerializeField, Range(6, 80)]
        private int _curvesSmoothness;

        [SerializeField, UsedImplicitly]
        private int _vertCount;

        [SerializeField, UsedImplicitly]
        private int _trianglesCount;
        
        [SerializeField]
        private bool _overrideStencil;
        
        [SerializeField]
        private CompareFunction _stencilComp;
        
        [SerializeField]
        private StencilOp _stencilOp;
    
        [SerializeField]
        private int _stencilRef;
        
        [SerializeField]
        private bool _hideMaskGraphics;
        
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
                    materialCopy.SetInt(StencilOp, (int)_stencilOp);
                    var colorMask = _hideMaskGraphics ? 0 : (int)ColorWriteMask.All;
                    materialCopy.SetInt(ColorMask, colorMask);
                    
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
            mat.SetInt(StencilOp, (int)_stencilOp);
            var colorMask = _hideMaskGraphics ? 0 : (int)ColorWriteMask.All;
            mat.SetInt(ColorMask, colorMask);
        }
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            ApplyStencil();
        }
#endif
        
        protected override void Start()
        {
            base.Start();
            
            ApplyStencil();
        }

        public Color EdgeColor
        {
            get => _edgeColor;
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
            get => color;
            set
            {
                if (color != value)
                {
                    color = value;
                    SetVerticesDirty();
                }
            }
        }
        
        public float EdgeThicknessAbsolute
        {
            get => _edgeThickness;
            set
            {
                var oldValue = _edgeThickness;
                var newValue = value;

                if (Math.Abs(newValue - oldValue) <= 0.0001f)
                {
                    return;
                }
                
                _edgeThickness = value;

                if (_edgeThicknessMode == EdgeThicknessMode.Relative)
                {
                    return;
                }
                
                SetVerticesDirty();
            }
        }
        
        public float EdgeThicknessRelative
        {
            get => _edgeThicknessRelative;
            set
            {
                var oldValue = _edgeThicknessRelative;
                var newValue = value;

                if (Math.Abs(newValue - oldValue) <= 0.0001f)
                {
                    return;
                }
                
                _edgeThicknessRelative = value;

                if (_edgeThicknessMode == EdgeThicknessMode.Absolute)
                {
                    return;
                }
                
                SetVerticesDirty();
            }
        }

        public void SetColors(Color newMainColor, Color newEdgeColor)
        {
            var isChanged = false;
            if (color != newMainColor)
            {
                isChanged = true;
                color = newMainColor;
            }

            if (_edgeColor != newEdgeColor)
            {
                isChanged = true;
                _edgeColor = newEdgeColor;
            }

            if (isChanged)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            _vertCount = 0;
            _trianglesCount = 0;

            Rect enclosingRect = GetPixelAdjustedRect();

            var smallerSide = Math.Min(enclosingRect.width, enclosingRect.height);
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

            var ellipseInfo = new EllipseInfo(
                enclosingRect,
                _peaksCount * 2,
                _startAngle * Mathf.Deg2Rad,
                _isDashed,
                _antiAliasingLayerWidth,
                edge,
                _innerAntiAliasingLayerWidth,
                _innerDisplacementFactor,
                _outerDisplacementFactor,
                _curvesSmoothness,
                _innerTangentPointShift,
                _outerTangentPointShift,
                _innerTangentPointShiftPerp,
                _outerTangentPointShiftPerp);

            vh.Clear();

            switch (_mode)
            {
                case Mode.FillInside:
                {
                    FillInside(vh, in ellipseInfo);
                    break;
                }
                case Mode.Edge:
                {
                    GenerateEdges(vh, in ellipseInfo);
                    break;
                }
                case Mode.EdgeAndFill:
                {
                    GenerateEdgesAndFill(vh, in ellipseInfo);
                    break;
                }
            }

            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }

        private void FillInside(VertexHelper vh, in EllipseInfo info)
        {
            int smoothness = info.CurvesSmoothness;
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections * smoothness, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<(Vector2 inner, Vector2 outer)> vectorsBuffer);

            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in info.EnclosingRect));

            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.FillInside);

            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();

            var innerShift = info.InnerTangentPointShift * info.InnerOuterDiff;
            var outerShift = info.OuterTangentPointShift * info.InnerOuterDiff;
            
            var innerShiftPerp = info.InnerTangentPointShiftPerp * info.InnerHalfSize;
            var outerShiftPerp = info.OuterTangentPointShiftPerp * info.OuterHalfSize;
            
            for (int i = 0; i < positions.Length; i++)
            {
                var isEven = (i & 1) == 0;

                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);

                var peakPosition = isEven ? item.inner : item.outer;
                var nextPeakPosition = !isEven ? nextItem.inner : nextItem.outer;
                var peakTangentPoint = ((isEven ? nextItem.inner : nextItem.outer) + peakPosition) / 2.0f;
                var nextPeakTangentPoint = ((!isEven ? item.inner : item.outer) + nextPeakPosition) / 2.0f;

                var tangentDirFromCenter = (peakTangentPoint - centerPosition).normalized;
                var nextTangentDirFromCenter = (nextPeakTangentPoint - centerPosition).normalized;

                var peakToCenterDirection = (centerPosition - peakPosition).normalized;
                var nextPeakToCenterDirection = (centerPosition - nextPeakPosition).normalized;

                peakTangentPoint += tangentDirFromCenter * (isEven ? innerShift : outerShift);
                nextPeakTangentPoint += nextTangentDirFromCenter * (!isEven ? innerShift : outerShift);
                
                peakTangentPoint += -Vector2.Perpendicular(tangentDirFromCenter) * (isEven ? innerShiftPerp : outerShiftPerp);
                nextPeakTangentPoint += Vector2.Perpendicular(nextTangentDirFromCenter) * (!isEven ? innerShiftPerp : outerShiftPerp);
                
                var distanceAa = info.AaWidth;
                var lastValue = smoothness - 1;

                for (int j = 0; j < smoothness; j++)
                {
                    var t = Mathf.InverseLerp(0, lastValue, j);
                    var pos = CubicBezier(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);
                    var bezierTangent = CalculateCubicBezierTangent(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);

                    var leftNormal = j switch
                    {
                        0 => peakToCenterDirection,
                        var x when x == lastValue => nextPeakToCenterDirection,
                        _ => Vector2.Perpendicular(bezierTangent).normalized,
                    };
                    
                    var rightNormal = -leftNormal;
                    
                    var posAa = pos + (rightNormal * distanceAa);

                    Vert vert = new Vert
                    {
                        Position = pos,
                        AaPosition = posAa,
                    };

                    AddHullVert(in vh, ref vert, ref buffer, in hullSettings);
                }
            }

            var filledVerts = buffer.FilledBufferSlice();

            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                vh.AddTriangle(0, vert.Index, prevVert.Index);

                if (info.IsAaEnabled)
                {
                    vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                    vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.Index);
                }
            }
        }

        private void GenerateEdges(VertexHelper vh, in EllipseInfo info)
        {
            int smoothness = info.CurvesSmoothness;

            var centerPosition = info.EnclosingRect.center;
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.Edge);

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections * smoothness, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<(Vector2 inner, Vector2 outer)> vectorsBuffer);

            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();
            
            var innerShift = info.InnerTangentPointShift * info.InnerOuterDiff;
            var outerShift = info.OuterTangentPointShift * info.InnerOuterDiff;
            
            var innerShiftPerp = info.InnerTangentPointShiftPerp * info.InnerHalfSize;
            var outerShiftPerp = info.OuterTangentPointShiftPerp * info.OuterHalfSize;

            for (int i = 0; i < positions.Length; i++)
            {
                var isEven = (i & 1) == 0;

                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);

                var peakPosition = isEven ? item.inner : item.outer;
                var nextPeakPosition = !isEven ? nextItem.inner : nextItem.outer;
                var peakTangentPoint = ((isEven ? nextItem.inner : nextItem.outer) + peakPosition) / 2.0f;
                var nextPeakTangentPoint = ((!isEven ? item.inner : item.outer) + nextPeakPosition) / 2.0f;

                var tangentDirFromCenter = (peakTangentPoint - centerPosition).normalized;
                var nextTangentDirFromCenter = (nextPeakTangentPoint - centerPosition).normalized;

                var peakToCenterDirection = (centerPosition - peakPosition).normalized;
                var nextPeakToCenterDirection = (centerPosition - nextPeakPosition).normalized;

                peakTangentPoint += tangentDirFromCenter * (isEven ? innerShift : outerShift);
                nextPeakTangentPoint += nextTangentDirFromCenter * (!isEven ? innerShift : outerShift);
                
                peakTangentPoint += -Vector2.Perpendicular(tangentDirFromCenter) * (isEven ? innerShiftPerp : outerShiftPerp);
                nextPeakTangentPoint += Vector2.Perpendicular(nextTangentDirFromCenter) * (!isEven ? innerShiftPerp : outerShiftPerp);
                
                var distanceAa = info.AaWidth;
                var distanceEdge = info.EdgeWidth;
                var distanceAaEdge = info.InnerAaWidth;
                var lastValue = smoothness - 1;
                
                for (int j = 0; j < smoothness; j++)
                {
                    var t = Mathf.InverseLerp(0, lastValue, j);
                    var pos = CubicBezier(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);
                    var bezierTangent = CalculateCubicBezierTangent(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);

                    var leftNormal = j switch
                    {
                        0 => peakToCenterDirection,
                        var x when x == lastValue => nextPeakToCenterDirection,
                        _ => Vector2.Perpendicular(bezierTangent).normalized,
                    };
                    
                    var rightNormal = -leftNormal;
                    
                    var posAa = pos + (rightNormal * distanceAa);
                    var posEdge = pos + (leftNormal * distanceEdge);
                    var posAaEdge = pos + (leftNormal * distanceAaEdge);

                    Vert vert = new Vert
                    {
                        Position = pos,
                        AaPosition = posAa,
                        EdgePosition = posEdge,
                        AaEdgePosition = posAaEdge,
                    };

                    AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
                }
            }

            var shouldDrawTriangles = true;
            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                if (shouldDrawTriangles)
                {
                    vh.AddTriangle(vert.AaEdgeIndex, vert.EdgeIndex, prevVert.EdgeIndex);
                    vh.AddTriangle(vert.AaEdgeIndex, prevVert.AaEdgeIndex, prevVert.EdgeIndex);

                    vh.AddTriangle(vert.EdgeIndex, vert.Index, prevVert.Index);
                    vh.AddTriangle(vert.EdgeIndex, prevVert.EdgeIndex, prevVert.Index);

                    if (info.IsAaEnabled)
                    {
                        vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                        vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.Index);
                    }
                }

                shouldDrawTriangles = !info.IsDashed || !shouldDrawTriangles;
            }
        }

        private void GenerateEdgesAndFill(VertexHelper vh, in EllipseInfo info)
        {
            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var centerColor = CalculateColor(in centerPosition, in colorData);
            var centerUv = CalculateUv(in centerPosition, in info.EnclosingRect);
            vh.AddVert(centerPosition, centerColor, centerUv);

            int smoothness = info.CurvesSmoothness;

            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.EdgeAndFill);

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections * smoothness, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<(Vector2 inner, Vector2 outer)> vectorsBuffer);

            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();
            
            var innerShift = info.InnerTangentPointShift * info.InnerOuterDiff;
            var outerShift = info.OuterTangentPointShift * info.InnerOuterDiff;
            
            var innerShiftPerp = info.InnerTangentPointShiftPerp * info.InnerHalfSize;
            var outerShiftPerp = info.OuterTangentPointShiftPerp * info.OuterHalfSize;
            
            for (int i = 0; i < positions.Length; i++)
            {
                var isEven = (i & 1) == 0;

                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);

                var peakPosition = isEven ? item.inner : item.outer;
                var nextPeakPosition = !isEven ? nextItem.inner : nextItem.outer;
                var peakTangentPoint = ((isEven ? nextItem.inner : nextItem.outer) + peakPosition) / 2.0f;
                var nextPeakTangentPoint = ((!isEven ? item.inner : item.outer) + nextPeakPosition) / 2.0f;

                var tangentDirFromCenter = (peakTangentPoint - centerPosition).normalized;
                var nextTangentDirFromCenter = (nextPeakTangentPoint - centerPosition).normalized;

                var peakToCenterDirection = (centerPosition - peakPosition).normalized;
                var nextPeakToCenterDirection = (centerPosition - nextPeakPosition).normalized;

                peakTangentPoint += tangentDirFromCenter * (isEven ? innerShift : outerShift);
                nextPeakTangentPoint += nextTangentDirFromCenter * (!isEven ? innerShift : outerShift);
                
                peakTangentPoint += -Vector2.Perpendicular(tangentDirFromCenter) * (isEven ? innerShiftPerp : outerShiftPerp);
                nextPeakTangentPoint += Vector2.Perpendicular(nextTangentDirFromCenter) * (!isEven ? innerShiftPerp : outerShiftPerp);
                
                var distanceAa = info.AaWidth;
                var distanceEdge = info.EdgeWidth;
                var distanceAaEdge = info.InnerAaWidth;
                var lastValue = smoothness - 1;
                
                for (int j = 0; j < smoothness; j++)
                {
                    var t = Mathf.InverseLerp(0, lastValue, j);
                    var pos = CubicBezier(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);
                    var bezierTangent = CalculateCubicBezierTangent(peakPosition, peakTangentPoint, nextPeakPosition, nextPeakTangentPoint, t);

                    var leftNormal = j switch
                    {
                        0 => peakToCenterDirection,
                        var x when x == lastValue => nextPeakToCenterDirection,
                        _ => Vector2.Perpendicular(bezierTangent).normalized,
                    };
                    
                    var rightNormal = -leftNormal;
                    
                    var posAa = pos + (rightNormal * distanceAa);
                    var posEdge = pos + (leftNormal * distanceEdge);
                    var posAaEdge = pos + (leftNormal * distanceAaEdge);

                    Vert vert = new Vert
                    {
                        Position = pos,
                        AaPosition = posAa,
                        EdgePosition = posEdge,
                        AaEdgePosition = posAaEdge,
                    };

                    AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
                }
            }

            var shouldDrawTriangles = true;
            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                vh.AddTriangle(0, vert.AaEdgeIndex, prevVert.AaEdgeIndex);

                vh.AddTriangle(vert.AaEdgeIndex, vert.EdgeIndex, prevVert.EdgeIndex);
                vh.AddTriangle(vert.AaEdgeIndex, prevVert.AaEdgeIndex, prevVert.EdgeIndex);

                if (shouldDrawTriangles)
                {
                    vh.AddTriangle(vert.EdgeIndex, vert.Index, prevVert.Index);
                    vh.AddTriangle(vert.EdgeIndex, prevVert.EdgeIndex, prevVert.Index);

                    if (info.IsAaEnabled)
                    {
                        vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                        vh.AddTriangle(prevVert.Index, vert.Index, prevVert.AaIndex);
                    }
                }

                shouldDrawTriangles = !info.IsDashed || !shouldDrawTriangles;
            }
        }

        private static void FillInPeakPositions(
            in EllipseInfo info,
            in Vector2 centerPosition,
            ref SpanBuffer<(Vector2 inner, Vector2 outer)> vectorsBuffer)
        {
            for (int i = 0; i < info.HullSections; i++)
            {
                float rotation = info.StartAngleRadians + (i * info.AngleDeltaRadians);
                var cos = Mathf.Cos(rotation);
                var sin = Mathf.Sin(rotation);

                var vecInner = centerPosition + new Vector2(info.InnerHalfSize.x * cos, info.InnerHalfSize.y * sin);
                var vecOuter = centerPosition + new Vector2(info.OuterHalfSize.x * cos, info.OuterHalfSize.y * sin);

                vectorsBuffer.Add((vecInner, vecOuter));
            }
        }

        private static Vector2 CalculatePointOnDistance(in Vector2 startPoint, in Vector2 rightVec, in Vector2 leftVec, float thickness)
        {
            Vector2 direction = -(rightVec + leftVec).normalized;
            var angle = Vector2.Angle(rightVec, leftVec);
            var angleRad = (90 - angle / 2.0f) * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleRad);
            var distance = thickness / cos;

            return startPoint + (direction * distance);
        }

        private void AddHullVertWithEdge(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings settings)
        {
            var mainColorData = new ColorCalculationData(settings.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var edgeColorData = new ColorCalculationData(
                settings.EnclosingRect,
                _edgeColor,
                _edgeColorGradient,
                _edgeGradientType,
                _shouldUseGradientForEdge);

            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(
                vertex.Position,
                CalculateColor(in vertex.Position, edgeColorData),
                CalculateUv(in vertex.Position, in settings.EnclosingRect));

            vertex.EdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(
                vertex.EdgePosition,
                CalculateColor(in vertex.EdgePosition, edgeColorData),
                CalculateUv(in vertex.EdgePosition, in settings.EnclosingRect));

            vertex.AaEdgeIndex = (short)vh.currentVertCount;
            var aaEdgeColor = settings.DrawingMode == Mode.Edge
                                  ? CalculateColor(in vertex.AaEdgePosition, edgeColorData).WithAlpha(0.0f)
                                  : CalculateColor(in vertex.AaEdgePosition, mainColorData);
            vh.AddVert(vertex.AaEdgePosition, aaEdgeColor, CalculateUv(in vertex.AaEdgePosition, in settings.EnclosingRect));

            if (settings.IsAaEnabled)
            {
                vertex.AaIndex = (short)vh.currentVertCount;
                vh.AddVert(
                    vertex.AaPosition,
                    CalculateColor(in vertex.AaPosition, edgeColorData).WithAlpha(0.0f),
                    CalculateUv(in vertex.AaPosition, in settings.EnclosingRect));
            }

            verts.Add(vertex);
        }

        private void AddHullVert(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings settings)
        {
            var colorData = new ColorCalculationData(settings.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var vertColor = CalculateColor(in vertex.Position, in colorData);
            var vertUv = CalculateUv(in vertex.Position, in settings.EnclosingRect);

            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);

            if (settings.IsAaEnabled)
            {
                vertex.AaIndex = (short)vh.currentVertCount;
                vh.AddVert(vertex.AaPosition, vertColor.WithAlpha(0.0f), vertUv);
            }

            verts.Add(vertex);
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
                    var halfWidth = rect.width / 2;
                    var centerPosition = new Vector2(rect.xMin + halfWidth, rect.yMax);

                    if (vertexPosition == centerPosition)
                    {
                        return colorData.GradientColor.Evaluate(0.0f);
                    }
                    else
                    {
                        return colorData.GradientColor.Evaluate(1.0f);
                    }
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

        private static Vector2 CubicBezier(Vector2 pointX, Vector2 tangentX, Vector2 pointY, Vector2 tangentY, float t)
        {
            var xtx = Vector2.Lerp(pointX, tangentX, t);
            var txty = Vector2.Lerp(tangentX, tangentY, t);
            var tyy = Vector2.Lerp(tangentY, pointY, t);

            var a = Vector2.Lerp(xtx, txty, t);
            var b = Vector2.Lerp(txty, tyy, t);

            var c = Vector2.Lerp(a, b, t);

            return c;
        }

        private Vector2 CalculateCubicBezierTangent(Vector2 pointX, Vector2 tangentX, Vector2 pointY, Vector2 tangentY, float t)
        {
            float oneMinusT = 1 - t;

            var result = 3f * oneMinusT * oneMinusT * (tangentX - pointX)
                       + 6f * t * oneMinusT * (tangentY - tangentX)
                       + 3f * t * t * (pointY - tangentY);

            return result.normalized;
        }
    }
}