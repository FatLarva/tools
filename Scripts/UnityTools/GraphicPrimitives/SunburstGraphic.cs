using System;
using JetBrains.Annotations;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/SunburstGraphic", 12)]
    public class SunburstGraphic : Image
    {
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
            
            public EllipseInfo(Rect enclosingRect, int hullSections, float startAngleRadians, bool isDashed, float? aaWidth, float? edgeWidth, float? innerAaWidth, float innerDisplacementFactor, float outerDisplacementFactor)
            {
                EnclosingRect = enclosingRect;
                HullSections = hullSections;
                StartAngleRadians = startAngleRadians;
                AngleDeltaRadians = 2 * Mathf.PI / hullSections;
                IsAaEnabled = aaWidth > 0;
                IsDashed = isDashed;
                
                AaWidth = aaWidth.GetValueOrDefault();
                EdgeWidth = edgeWidth.GetValueOrDefault();
                InnerAaWidth = EdgeWidth + innerAaWidth.GetValueOrDefault();
                
                InnerHalfSize = new Vector2(enclosingRect.width / 2, enclosingRect.height / 2) * innerDisplacementFactor;
                OuterHalfSize = new Vector2(enclosingRect.width / 2, enclosingRect.height / 2) * outerDisplacementFactor;
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
        private Gradient _gradientColor = new ();
        
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
                _outerDisplacementFactor); 

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

        // Uncomment for precise raycasts/clicks (might be processor-heavy)
        //public override bool Raycast( Vector2 sp, Camera eventCamera )
        //{
        //	if( base.Raycast( sp, eventCamera ) )
        //	{
        //		Vector2 localPoint;
        //		if( RectTransformUtility.ScreenPointToLocalPointInRectangle( rectTransform, sp, eventCamera, out localPoint ) )
        //		{
        //			Vector2 deltaPoint = localPoint - rectTransform.rect.center;
        //			float distance = deltaPoint.sqrMagnitude;

        //			float angle = Vector2.Angle( Vector2.right, new Vector2( deltaPoint.x / width, deltaPoint.y / height ) ) * Mathf.Deg2Rad;
        //			Vector2 edge = new Vector2( width * Mathf.Cos( angle ), height * Mathf.Sin( angle ) );

        //			if( mode == Mode.FillInside )
        //				return edge.sqrMagnitude >= distance;
        //			if( mode == Mode.FillOutside )
        //				return edge.sqrMagnitude <= distance;

        //			if( edge.sqrMagnitude < distance )
        //				return false;

        //			angle = Vector2.Angle( Vector2.right, new Vector2( deltaPoint.x / ( width - edgeThickness ), deltaPoint.y / ( height - edgeThickness ) ) ) * Mathf.Deg2Rad;
        //			Vector2 edgeInner = new Vector2( ( width - edgeThickness ) * Mathf.Cos( angle ), ( height - edgeThickness ) * Mathf.Sin( angle ) );
        //			Debug.DrawLine( transform.position, transform.position + (Vector3) edgeInner );
        //			Debug.DrawLine( transform.position, transform.position + (Vector3) deltaPoint );
        //			return edgeInner.sqrMagnitude <= distance;
        //		}
        //	}

        //	return false;
        //}
        
        private void FillInside(VertexHelper vh, in EllipseInfo info)
        {
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vector2> vectorsBuffer);

            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in info.EnclosingRect));
            
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.FillInside);

            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();
            
            for (int i = 0; i < positions.Length; i++)
            {
                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);
                var rightHand = prevItem - item;
                var leftHand = nextItem - item;

                var distance = (i & 1) == 0 ? -info.AaWidth : info.AaWidth;
                
                Vert vert = new Vert
                {
                    Position = item,
                    AaPosition = CalculatePointOnDistance(item, rightHand, leftHand, distance),
                };

                AddHullVert(in vh, ref vert, ref buffer, in hullSettings);
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
            var centerPosition = info.EnclosingRect.center;
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.Edge);
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vector2> vectorsBuffer);
            
            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();
            
            for (int i = 0; i < positions.Length; i++)
            {
                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);
                var rightHand = prevItem - item;
                var leftHand = nextItem - item;

                var isEven = (i & 1) == 0;
                
                var distanceAa = isEven ? -info.AaWidth : info.AaWidth;
                var distanceEdge = isEven ? info.EdgeWidth : -info.EdgeWidth;
                var distanceAaEdge = isEven ? info.InnerAaWidth : -info.InnerAaWidth;
                
                Vert vert = new Vert
                {
                    Position = item,
                    AaPosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceAa),
                    EdgePosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceEdge),
                    AaEdgePosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceAaEdge),
                };

                AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
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
                        vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.AaIndex);
                        vh.AddTriangle(prevVert.Index, vert.AaIndex, vert.Index);
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

            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.EdgeAndFill);
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);
            using var selfOwnedVectorsBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vector2> vectorsBuffer);
            
            FillInPeakPositions(info, centerPosition, ref vectorsBuffer);
            var positions = vectorsBuffer.FilledBufferSlice();
            
            for (int i = 0; i < positions.Length; i++)
            {
                var (item, prevItem, nextItem) = positions.GetCircularConsecutiveTrio(i);
                var rightHand = prevItem - item;
                var leftHand = nextItem - item;

                var isEven = (i & 1) == 0;
                
                var distanceAa = isEven ? -info.AaWidth : info.AaWidth;
                var distanceEdge = isEven ? info.EdgeWidth : -info.EdgeWidth;
                var distanceAaEdge = isEven ? info.InnerAaWidth : -info.InnerAaWidth;
                
                Vert vert = new Vert
                {
                    Position = item,
                    AaPosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceAa),
                    EdgePosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceEdge),
                    AaEdgePosition = CalculatePointOnDistance(item, rightHand, leftHand, distanceAaEdge),
                };

                AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
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
                        vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.AaIndex);
                        vh.AddTriangle(prevVert.Index, vert.AaIndex, vert.Index);
                    }
                }
                
                shouldDrawTriangles = !info.IsDashed || !shouldDrawTriangles;
            }
        }
        
        private static void FillInPeakPositions(in EllipseInfo info, in Vector2 centerPosition, ref SpanBuffer<Vector2> vectorsBuffer)
        {
            for (int i = 0; i < info.HullSections; i++)
            {
                float rotation = info.StartAngleRadians + (i * info.AngleDeltaRadians);
                var cos = Mathf.Cos(rotation);
                var sin = Mathf.Sin(rotation);

                var halfSize = (i & 1) == 0 ? info.InnerHalfSize : info.OuterHalfSize;
                var vec = centerPosition + new Vector2(halfSize.x * cos, halfSize.y * sin);
                
                vectorsBuffer.Add(vec);
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
            var edgeColorData = new ColorCalculationData(settings.EnclosingRect, _edgeColor, _edgeColorGradient, _edgeGradientType, _shouldUseGradientForEdge);

            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, CalculateColor(in vertex.Position, edgeColorData), CalculateUv(in vertex.Position, in settings.EnclosingRect));
            
            vertex.EdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.EdgePosition, CalculateColor(in vertex.EdgePosition, edgeColorData), CalculateUv(in vertex.EdgePosition, in settings.EnclosingRect));
            
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
    }
}
