/*
using System;
using System.Buffers;

namespace UnityTools
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/InversedEllipseGraphic", 12)]
    public class InversedEllipseGraphic : MaskableGraphic
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
            public readonly bool IsEdge;

            public HullSettings(Rect enclosingRect, bool isAaEnabled, bool isEdge)
            {
                EnclosingRect = enclosingRect;
                IsAaEnabled = isAaEnabled;
                IsEdge = isEdge;
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
            public readonly bool IsAaEnabled;
            public readonly bool IsDashed;
            
            public EllipseInfo(Rect enclosingRect, int hullSections, float startAngleRadians, bool isDashed, float? aaWidth, float? edgeWidth, float? innerAaWidth)
            {
                EnclosingRect = enclosingRect;
                HullSections = hullSections;
                StartAngleRadians = startAngleRadians;
                AngleDeltaRadians = 2 * Mathf.PI / hullSections;
                IsAaEnabled = aaWidth > 0;
                IsDashed = isDashed;
                
                EllipseHalfSize = new Vector2(enclosingRect.width / 2, enclosingRect.height / 2);
                EllipseAaHalfSize = aaWidth.HasValue ? EllipseHalfSize + new Vector2(aaWidth.Value, aaWidth.Value) : Vector2.zero;
                EllipseEdgeHalfSize = edgeWidth.HasValue ? EllipseHalfSize - new Vector2(edgeWidth.Value, edgeWidth.Value) : Vector2.zero;
                EllipseEdgeAaHalfSize = innerAaWidth.HasValue ? EllipseEdgeHalfSize - new Vector2(innerAaWidth.Value, innerAaWidth.Value) : Vector2.zero;
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
            Fill = 1,
            Edge = 2,
            EdgeAndFill = 3,
        };

        private enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        };

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
        
        [SerializeField, Range(0, 100)]
        [Tooltip("When greater than 0 adds thin mesh edge around generated mesh with alpha transitioning from 1 to 0 so it'll look smoother.")]
        private float _antiAliasingLayerWidth = 0;
        
        [SerializeField, Range(0, 100)]
        [Tooltip("Thickness of the line where edge color interpolates into mainColor")]
        private float _innerAaThickness = 0;
        
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
        
        public void SetColors(Color newMainColor, Color newEdgeColor)
        {
            color = newMainColor;
            _edgeColor = newEdgeColor;
            
            SetVerticesDirty();
        }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect r = GetPixelAdjustedRect();
            Rect enclosingRect = new Rect(r);
            
            var smallerSide = Math.Min(r.width, r.height);
            var halfSmallerSide = smallerSide * 0.5f;
            if (_keepCircle)
            {
                enclosingRect = new Rect(r.center - new Vector2(halfSmallerSide, halfSmallerSide), new Vector2(smallerSide, smallerSide));
                
                width = smallerSide;
                height = smallerSide;
            }

            aaWidth = width + _antiAliasingLayerWidth;
            aaHeight = height + _antiAliasingLayerWidth;

            vh.Clear();

            Vector2 pivot = rectTransform.pivot;
            deltaWidth = r.width * (0.5f - pivot.x);
            deltaHeight = r.height * (0.5f - pivot.y);
            
            var edge = _edgeThicknessMode switch
            {
                EdgeThicknessMode.Absolute => _edgeThickness,
                EdgeThicknessMode.Relative => _edgeThicknessRelative * halfSmallerSide,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(EdgeThicknessMode)}: {_edgeThicknessMode}"),
            };

            var ellipseInfo = new EllipseInfo(
                enclosingRect,
                detail,
                startAngle * Mathf.Deg2Rad,
                isDashed,
                _antiAliasingLayerWidth,
                edge,
                _innerAaThickness); 

            switch (_mode)
            {
                case Mode.FillInside:
                {
                    FillInside(vh, in ellipseInfo);
                    break;
                }
                case Mode.Fill:
                {
                    int quarterDetail = (detail + 3) / 4;
                    deltaRadians = 360f / (quarterDetail * 4) * Mathf.Deg2Rad;
                    GenerateOutside(vh, r, quarterDetail);
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
            using var selfOwnedBuffer = RentBufferForVerts(info.HullSections, out SpanBuffer<Vert> buffer);

            // Center vert
            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in info.EnclosingRect));
            
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, false);

            for (int i = 0; i < info.HullSections; i++)
            {
                float rotation = info.StartAngleRadians + (i * info.AngleDeltaRadians);
                var cos = Mathf.Cos(rotation);
                var sin = Mathf.Sin(rotation);

                var vert = new Vert
                {
                    Position = centerPosition + new Vector2(info.EllipseHalfSize.x * cos, info.EllipseHalfSize.y * sin),
                    AaPosition = info.IsAaEnabled ? centerPosition + new Vector2(info.EllipseAaHalfSize.x * cos, info.EllipseAaHalfSize.y * sin) : Vector2.zero,
                };
                
                AddHullVert(in vh, ref vert, ref buffer, in hullSettings);
            }
            
            var filledVerts = buffer.FilledBufferSlice();

            for (int i = 0; i <= filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = GetConsecutiveVertsPair(filledVerts, i);

                vh.AddTriangle(0, vert.Index, prevVert.Index);

                if (info.IsAaEnabled)
                {
                    vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                    vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.Index);
                }
            }
        }

        private void GenerateOutside(VertexHelper vh, Rect r, int quarterDetail)
        {
            vh.AddVert(new Vector3(r.xMax, r.yMax, 0f), color, uv);
            vh.AddVert(new Vector3(r.xMin, r.yMax, 0f), color, uv);
            vh.AddVert(new Vector3(r.xMin, r.yMin, 0f), color, uv);
            vh.AddVert(new Vector3(r.xMax, r.yMin, 0f), color, uv);

            Span<int> jointPoints = stackalloc int[4];

            int triangleIndex = 4;
            jointPoints[0] = triangleIndex;
            FillOutside(vh, new Vector3(width + deltaWidth, deltaHeight, 0f), 0, quarterDetail, ref triangleIndex);
            jointPoints[1] = triangleIndex;
            FillOutside(vh, new Vector3(deltaWidth, height + deltaHeight, 0f), 1, quarterDetail, ref triangleIndex);
            jointPoints[2] = triangleIndex;
            FillOutside(vh, new Vector3(-width + deltaWidth, deltaHeight, 0f), 2, quarterDetail, ref triangleIndex);
            jointPoints[3] = triangleIndex;
            FillOutside(vh, new Vector3(deltaWidth, -height + deltaHeight, 0f), 3, quarterDetail, ref triangleIndex);

            if (_keepCircle && !Mathf.Approximately(r.width, r.height))
            {
                if (r.width > r.height)
                {
                    vh.AddTriangle(jointPoints[0], 3, 0);
                    vh.AddTriangle(jointPoints[2], 1, 2);
                }
                else
                {
                    vh.AddTriangle(jointPoints[1], 0, 1);
                    vh.AddTriangle(jointPoints[3], 2, 3);
                }
            }
        }
        
        private void FillOutside(VertexHelper vh, Vector3 initialPoint, int quarterIndex, int detailCount, ref int triangleIndex)
        {
            int startIndex = quarterIndex * detailCount;
            int endIndex = (quarterIndex + 1) * detailCount;

            vh.AddVert(initialPoint, color, uv);
            triangleIndex++;

            for (int i = startIndex + 1; i <= endIndex; i++, triangleIndex++)
            {
                float radians = i * deltaRadians;

                vh.AddVert(new Vector3(Mathf.Cos(radians) * width + deltaWidth, Mathf.Sin(radians) * height + deltaHeight, 0f), color, uv);
                vh.AddTriangle(quarterIndex, triangleIndex - 1, triangleIndex);
            }
        }

        private void GenerateEdges(VertexHelper vh, in EllipseInfo info)
        {
            var centerPosition = info.EnclosingRect.center;
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, true);
            
            using var selfOwnedBuffer = RentBufferForVerts(info.HullSections, out var buffer);
            
            for (int i = 0; i < info.HullSections; i++)
            {
                float rotation = info.StartAngleRadians + (i * info.AngleDeltaRadians);
                var cos = Mathf.Cos(rotation);
                var sin = Mathf.Sin(rotation);

                var vert = new Vert
                {
                    Position = centerPosition + new Vector2(info.EllipseHalfSize.x * cos, info.EllipseHalfSize.y * sin),
                    AaPosition = info.IsAaEnabled ? centerPosition + new Vector2(info.EllipseAaHalfSize.x * cos, info.EllipseAaHalfSize.y * sin) : Vector2.zero,
                    EdgePosition = centerPosition + new Vector2(info.EllipseEdgeHalfSize.x * cos, info.EllipseEdgeHalfSize.y * sin),
                    AaEdgePosition = centerPosition + new Vector2(info.EllipseEdgeAaHalfSize.x * cos, info.EllipseEdgeAaHalfSize.y * sin),
                };
                
                AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
            }
            
            var shouldDrawTriangles = true;
            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = GetConsecutiveVertsPair(filledVerts, i);

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
        
        private void GenerateEdgesAndFill(VertexHelper vh, in EllipseInfo info)
        {
            // Center vert
            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var centerColor = CalculateColor(in centerPosition, in colorData);
            var centerUv = CalculateUv(in centerPosition, in info.EnclosingRect);
            vh.AddVert(centerPosition, centerColor, centerUv);

            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, true);
            
            using var selfOwnedBuffer = RentBufferForVerts(info.HullSections, out var buffer);
            
            for (int i = 0; i < info.HullSections; i++)
            {
                float rotation = info.StartAngleRadians + (i * info.AngleDeltaRadians);
                var cos = Mathf.Cos(rotation);
                var sin = Mathf.Sin(rotation);

                var vert = new Vert
                {
                    Position = centerPosition + new Vector2(info.EllipseHalfSize.x * cos, info.EllipseHalfSize.y * sin),
                    AaPosition = info.IsAaEnabled ? centerPosition + new Vector2(info.EllipseAaHalfSize.x * cos, info.EllipseAaHalfSize.y * sin) : Vector2.zero,
                    EdgePosition = centerPosition + new Vector2(info.EllipseEdgeHalfSize.x * cos, info.EllipseEdgeHalfSize.y * sin),
                    AaEdgePosition = centerPosition + new Vector2(info.EllipseEdgeAaHalfSize.x * cos, info.EllipseEdgeAaHalfSize.y * sin),
                };
                
                AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings);
            }
            
            var shouldDrawTriangles = true;
            var filledVerts = buffer.FilledBufferSlice();
            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = GetConsecutiveVertsPair(filledVerts, i);

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

        private (Vert vert, Vert prevVert) GetConsecutiveVertsPair(Span<Vert> filledVerts, int i)
        {
            var index = i % filledVerts.Length;
            var prevIndex = i - 1;
            prevIndex = prevIndex < 0 ? filledVerts.Length + prevIndex : prevIndex;

            var vert = filledVerts[index];
            var prevVert = filledVerts[prevIndex];
            
            return (vert, prevVert);
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
            vh.AddVert(vertex.AaEdgePosition, CalculateColor(in vertex.AaEdgePosition, mainColorData), CalculateUv(in vertex.AaEdgePosition, in settings.EnclosingRect));
            
            if (settings.IsAaEnabled)
            {
                vertex.AaIndex = (short)vh.currentVertCount;
                vh.AddVert(
                    vertex.AaPosition,
                    CalculateColor(in vertex.AaPosition, mainColorData).WithAlpha(0.0f),
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
        
        private static SelfOwnedSpanBuffer<Vert> RentBufferForVerts(int maxVerts, out SpanBuffer<Vert> spanBuffer)
        {
            var memoryOwner = MemoryPool<Vert>.Shared.Rent(maxVerts);
            Span<Vert> verts = memoryOwner.Memory.Slice(0, maxVerts).Span;

            var selfOwnedBuffer = new SelfOwnedSpanBuffer<Vert>(verts, 0, memoryOwner);
            spanBuffer = selfOwnedBuffer.Buffer;
            
            return selfOwnedBuffer;
        }
    }
}
*/
