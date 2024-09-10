using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Triangle Graphic", 12)]
    public class TriangleGraphic : MaskableGraphic
    {
        private struct Vert
        {
            public Vector2 Position;
            public Vector2 AaPosition;
            public Vector2 EdgePosition;
            public Vector2 AaEdgePosition;
            public bool IsRounding;
            public Vector2 ControlPosition;
            public Vector2 ControlAaPosition;
            public Vector2 ControlEdgePosition;
            public Vector2 ControlAaEdgePosition;
            public short Index;
            public short AaIndex;
            public short EdgeIndex;
            public short AaEdgeIndex;
        }
        
        private readonly struct Sizes
        {
            public readonly float AaEdgeWidth;
            public readonly float EdgeWidth;
            public readonly float AaWidth;

            public float FirstContour => AaEdgeWidth;
            public float SecondContour => AaEdgeWidth + EdgeWidth;
            public float ThirdContour => AaEdgeWidth + EdgeWidth + AaWidth;
            
            public Sizes(float aaEdgeWidth, float edgeWidth, float aaWidth)
            {
                AaEdgeWidth = aaEdgeWidth;
                EdgeWidth = edgeWidth;
                AaWidth = aaWidth;
            }
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
        
        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }
        
        [SerializeField, Min(0)]
        private float _roundingRadiusX = 0.1f;
        
        [SerializeField, Min(0)]
        private float _roundingRadiusY = 0.1f;
        
        [SerializeField, Range(3, 50)]
        private int _roundingQuality = 3;
        
        [SerializeField]
        private bool _shouldUseGradient;
        
        [SerializeField]
        private GradientType _gradientType;
        
        [SerializeField]
        private Gradient _gradientColor = new ();
        
        [SerializeField, Min(0)]
        private float _edgeThickness = 1;
        
        [SerializeField, Range(0, 100)]
        private float _antiAliasingLayerWidth = 0;
        
        [SerializeField, Range(0, 100)]
        private float _innerAntiAliasingLayerWidth = 0;
        
        [SerializeField]
        private Color _edgeColor;
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(6 + _roundingQuality * 3, out SpanBuffer<Vert> buffer);
            var rect = GetPixelAdjustedRect();
            var sectionsInRounding = _roundingQuality;
            var roundingRadiusX = _roundingRadiusX * rect.width;
            var roundingRadiusY = _roundingRadiusY * rect.height;

            var sizes = new Sizes(_innerAntiAliasingLayerWidth, _edgeThickness, _antiAliasingLayerWidth);
            
            var aaEnabled = sizes.AaWidth > 0;
            var edgeEnabled = sizes.EdgeWidth > 0;
            
            var centerPosition = rect.center;
            var colorData = new ColorCalculationData(rect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in rect, default));
            
            var hullSettings = new HullSettings(rect, aaEnabled, edgeEnabled);
            
            var top = new Vector2(rect.xMin, rect.yMax);
            var bottom = new Vector2(rect.xMin, rect.yMin);
            var right = new Vector2(rect.xMax, rect.center.y);

            var top2Right = (right - top).normalized;
            var right2Bottom = (bottom - right).normalized;
            var bottom2Top = (top - bottom).normalized;
            
            Span<Vert> cornersVerts = stackalloc Vert[]
            {
                new Vert // Top-left
                {
                    Position = top + (-bottom2Top * roundingRadiusY),
                    AaEdgePosition = top + (-bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.FirstContour),
                    EdgePosition = top + (-bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.SecondContour),
                    AaPosition = top + (-bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.ThirdContour),
                    IsRounding = true,
                },
                new Vert // Top-right
                {
                    Position = top + (top2Right * roundingRadiusY),
                    AaEdgePosition = top + (top2Right * roundingRadiusY) + (Vector2.Perpendicular(top2Right) * sizes.FirstContour),
                    EdgePosition = top + (top2Right * roundingRadiusY) + (Vector2.Perpendicular(top2Right) * sizes.SecondContour),
                    AaPosition = top + (top2Right * roundingRadiusY) + (Vector2.Perpendicular(top2Right) * sizes.ThirdContour),
                },
                new Vert // Right-top
                {
                    Position = right + (-top2Right * roundingRadiusX),
                    AaEdgePosition = right + (-top2Right * roundingRadiusX) + (Vector2.Perpendicular(top2Right) * sizes.FirstContour),
                    EdgePosition = right + (-top2Right * roundingRadiusX) + (Vector2.Perpendicular(top2Right) * sizes.SecondContour),
                    AaPosition = right + (-top2Right * roundingRadiusX) + (Vector2.Perpendicular(top2Right) * sizes.ThirdContour),
                    IsRounding = true,
                },
                new Vert // Right-bottom
                {
                    Position = right + (right2Bottom * roundingRadiusX),
                    AaEdgePosition = right + (right2Bottom * roundingRadiusX) + (Vector2.Perpendicular(right2Bottom) * sizes.FirstContour),
                    EdgePosition = right + (right2Bottom * roundingRadiusX) + (Vector2.Perpendicular(right2Bottom) * sizes.SecondContour),
                    AaPosition = right + (right2Bottom * roundingRadiusX) + (Vector2.Perpendicular(right2Bottom) * sizes.ThirdContour),
                },
                new Vert // Bottom-right
                {
                    Position = bottom + (-right2Bottom * roundingRadiusY),
                    AaEdgePosition = bottom + (-right2Bottom * roundingRadiusY) + (Vector2.Perpendicular(right2Bottom) * sizes.FirstContour),
                    EdgePosition = bottom + (-right2Bottom * roundingRadiusY) + (Vector2.Perpendicular(right2Bottom) * sizes.SecondContour),
                    AaPosition = bottom + (-right2Bottom * roundingRadiusY) + (Vector2.Perpendicular(right2Bottom) * sizes.ThirdContour),
                    IsRounding = true,
                },
                new Vert // Bottom-left
                {
                    Position = bottom + (bottom2Top * roundingRadiusY),
                    AaEdgePosition = bottom + (bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.FirstContour),
                    EdgePosition = bottom + (bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.SecondContour),
                    AaPosition = bottom + (bottom2Top * roundingRadiusY) + (Vector2.Perpendicular(bottom2Top) * sizes.ThirdContour),
                },
            };

            CalculateControlPoints(ref cornersVerts, in rect, in sizes);

            for (var i = 0; i < cornersVerts.Length; i++)
            {
                ref var vert = ref cornersVerts[i];
                if (edgeEnabled)
                {
                    AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings, default);
                }
                else
                {
                    AddHullVert(in vh, ref vert, ref buffer, in hullSettings, default);
                }

                if (vert.IsRounding)
                {
                    ref var nextVert = ref cornersVerts[i + 1];
                    for (int j = 1; j < sectionsInRounding; j++)
                    {
                        var t = (1.0f / sectionsInRounding) * j;

                        var splinePoint = Bezier(vert.Position, nextVert.Position, vert.ControlPosition, t);
                        var splinePointInnerEdge = Bezier(vert.AaEdgePosition, nextVert.AaEdgePosition, vert.ControlAaEdgePosition, t);
                        var splinePointEdge = Bezier(vert.EdgePosition, nextVert.EdgePosition, vert.ControlEdgePosition, t);
                        var splinePointAntiAliasing = Bezier(vert.AaPosition, nextVert.AaPosition, vert.ControlAaPosition, t);

                        var roundingVert = new Vert
                        {
                            Position = splinePoint,
                            AaEdgePosition = splinePointInnerEdge,
                            EdgePosition = splinePointEdge,
                            AaPosition = splinePointAntiAliasing,
                        };

                        if (edgeEnabled)
                        {
                            AddHullVertWithEdge(in vh, ref roundingVert, ref buffer, in hullSettings, default);
                        }
                        else
                        {
                            AddHullVert(in vh, ref roundingVert, ref buffer, in hullSettings, default);
                        }
                    }
                }
            }
            
            var filledVerts = buffer.FilledBufferSlice();

            for (int i = 0; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);
                
                vh.AddTriangle(0, vert.Index, prevVert.Index);

                if (edgeEnabled)
                {
                    vh.AddTriangle(vert.Index, vert.AaEdgeIndex, prevVert.AaEdgeIndex);
                    vh.AddTriangle(prevVert.Index, prevVert.AaEdgeIndex, vert.Index);
                    
                    vh.AddTriangle(vert.AaEdgeIndex, vert.EdgeIndex, prevVert.EdgeIndex);
                    vh.AddTriangle(prevVert.AaEdgeIndex, prevVert.EdgeIndex, vert.AaEdgeIndex);
                }
                
                if (aaEnabled)
                {
                    if (edgeEnabled)
                    {
                        vh.AddTriangle(vert.EdgeIndex, vert.AaIndex, prevVert.AaIndex);
                        vh.AddTriangle(prevVert.EdgeIndex, prevVert.AaIndex, vert.EdgeIndex);
                    }
                    else
                    {
                        vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                        vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.Index);
                    }
                }
            }
        }

        private void CalculateControlPoints(ref Span<Vert> cornersVerts, in Rect rect, in Sizes sizes)
        {
            var top = new Vector2(rect.xMin, rect.yMax);
            var bottom = new Vector2(rect.xMin, rect.yMin);
            var right = new Vector2(rect.xMax, rect.center.y);

            var top2Right = (right - top).normalized;
            var right2Bottom = (bottom - right).normalized;
            var bottom2Top = (top - bottom).normalized;

            CalcAndSupply(ref cornersVerts, 0, top, -bottom2Top, top2Right, sizes);
            CalcAndSupply(ref cornersVerts, 2, right, -top2Right, right2Bottom, sizes);
            CalcAndSupply(ref cornersVerts, 4, bottom, -right2Bottom, bottom2Top, sizes);
            
            static void CalcAndSupply(ref Span<Vert> verts, int index, in Vector2 startPoint, in Vector2 rightVec, in Vector2 leftVec, in Sizes sizes)
            {
                Vector2 direction = -(rightVec + leftVec).normalized;
                var angle = Vector2.Angle(rightVec, leftVec);
                var angleRad = (90 - angle / 2.0f) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angleRad);
                var aaEdgeDistance = sizes.FirstContour / cos;
                var edgeDistance = sizes.SecondContour / cos;
                var aaDistance = sizes.ThirdContour / cos;

                ref Vert v = ref verts[index];
                
                v.ControlPosition = startPoint;
                v.ControlAaEdgePosition = startPoint + (direction * aaEdgeDistance);
                v.ControlEdgePosition = startPoint + (direction * edgeDistance);
                v.ControlAaPosition = startPoint + (direction * aaDistance);
            }
        }

        private Vector2 Bezier(Vector2 pointX, Vector2 pointY, Vector2 controlPoint, float t)
        {
            var a = Vector2.Lerp(pointX, controlPoint, t);
            var b = Vector2.Lerp(controlPoint, pointY, t);
            var c = Vector2.Lerp(a, b, t);
            
            return c;
        }

        /*private UvRect RecalculateSpriteUvsIfNeeded(Sprite sourceSprite, ref Sprite uvRectCalculatedForSprite, ref UvRect lastUvRect)
        {
            if (!lastUvRect.IsEmpty && sourceSprite == uvRectCalculatedForSprite)
            {
                return lastUvRect;
            }
            
            if (sourceSprite == null || sourceSprite.packed == false)
            {
                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = default;
                
                return lastUvRect;
            }

            if (sprite.packingMode == SpritePackingMode.Tight || sprite.uv.Length > 4)
            {
                var bottomLeft = new Vector2(float.MaxValue, float.MaxValue);
                var topRight = new Vector2(float.MinValue, float.MinValue);

                foreach (var uv in sprite.uv)
                {
                    bottomLeft.x = Mathf.Min(bottomLeft.x, uv.x);
                    bottomLeft.y = Mathf.Min(bottomLeft.y, uv.y);
                    
                    topRight.x = Mathf.Max(topRight.x, uv.x);
                    topRight.y = Mathf.Max(topRight.y, uv.y);
                }
                
                Debug.LogWarning($"Packed sprites with {nameof(SpritePackingMode.Tight)} packing mode may appear wrongly. Consider using unpacked sprite or {nameof(SpritePackingMode.Rectangle)} packing.");

                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = new UvRect(bottomLeft, topRight);
                
                return lastUvRect;
            }
            else
            {
                var uvs = sprite.uv;
                var topRight = uvs[1];
                var bottomLeft = uvs[2];
            
                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = new UvRect(bottomLeft, topRight);
                
                return lastUvRect;
            }
        }*/
        
        private void AddHullVert(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings settings, in UvRect uvRect)
        {
            var colorData = new ColorCalculationData(settings.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var vertColor = CalculateColor(in vertex.Position, in colorData);
            var vertUv = CalculateUv(in vertex.Position, in settings.EnclosingRect, in uvRect);
            
            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            if (settings.IsAaEnabled)
            {
                vertex.AaIndex = (short)vh.currentVertCount;
                vh.AddVert(vertex.AaPosition, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private void AddHullVertWithEdge(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings settings, in UvRect uvRect)
        {
            var mainColorData = new ColorCalculationData(settings.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var edgeColorData = new ColorCalculationData(settings.EnclosingRect, _edgeColor, _gradientColor, _gradientType, _shouldUseGradient);

            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, CalculateColor(in vertex.Position, mainColorData), CalculateUv(in vertex.Position, in settings.EnclosingRect, in uvRect));
            
            vertex.AaEdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.AaEdgePosition, CalculateColor(in vertex.AaEdgePosition, edgeColorData), CalculateUv(in vertex.AaEdgePosition, in settings.EnclosingRect, in uvRect));
            
            vertex.EdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.EdgePosition, CalculateColor(in vertex.EdgePosition, edgeColorData), CalculateUv(in vertex.EdgePosition, in settings.EnclosingRect, in uvRect));
            
            if (settings.IsAaEnabled)
            {
                vertex.AaIndex = (short)vh.currentVertCount;
                vh.AddVert(
                    vertex.AaPosition,
                    CalculateColor(in vertex.AaPosition, edgeColorData).WithAlpha(0.0f),
                    CalculateUv(in vertex.AaPosition, in settings.EnclosingRect, in uvRect));
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

        private Vector4 CalculateUv(in Vector2 vertexPosition, in Rect enclosingRect, in UvRect spriteUvs)
        {
            var u = (vertexPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (vertexPosition.y - enclosingRect.yMin) / enclosingRect.height;

            if (spriteUvs.IsEmpty)
            {
                return new Vector2(u, v);
            }

            var spriteAdjustedU = Mathf.Lerp(spriteUvs.BottomLeft.x, spriteUvs.TopRight.x, u);
            var spriteAdjustedV = Mathf.Lerp(spriteUvs.BottomLeft.y, spriteUvs.TopRight.y, v);
            
            return new Vector2(spriteAdjustedU, spriteAdjustedV);
        }

#if UNITY_EDITOR
        // If the component changes in the inspector, update the mesh
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
