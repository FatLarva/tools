using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/SplineTest Graphic", 12)]
    public class SplineTestGraphic : MaskableGraphic
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
        
        /*[SerializeField, Min(0)]
        private float _roundingRadius = 0.1f;*/
        
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
        
        [SerializeField]
        private float _distanceMultiplier;
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(6 + _roundingQuality * 3, out SpanBuffer<Vert> buffer);
            var rect = GetPixelAdjustedRect();
            var edge = _edgeThickness;
            var aaWidth = _antiAliasingLayerWidth;
            var innerAaWidth = _innerAntiAliasingLayerWidth;
            var sectionsInRounding = _roundingQuality;
            
            var aaEnabled = aaWidth > 0;
            var edgeEnabled = edge > 0;
            
            var centerPosition = new Vector2(rect.center.x, rect.yMin);
            var colorData = new ColorCalculationData(rect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in rect, default));
            
            var hullSettings = new HullSettings(rect, aaEnabled, edgeEnabled);

            var baseDistance = rect.width * _distanceMultiplier;
            
            var a = new Vector2(rect.center.x - baseDistance, rect.yMin);
            var b = new Vector2(rect.center.x + baseDistance, rect.yMin);
            var c = rect.center;

            Span<Vert> cornersVerts = stackalloc Vert[]
            {
                new Vert // Top-left
                {
                    Position = a,
                    AaEdgePosition = a + (Vector2.left * innerAaWidth),
                    EdgePosition = a + (Vector2.left * (innerAaWidth + edge)),
                    AaPosition = a + (Vector2.left * (innerAaWidth + edge + aaWidth)),
                    ControlPosition = c,
                    ControlAaEdgePosition = c + (Vector2.up * innerAaWidth),
                    ControlEdgePosition = c + (Vector2.up * (innerAaWidth + edge)),
                    ControlAaPosition = c + (Vector2.up * (innerAaWidth + edge + aaWidth)),
                    IsRounding = true,
                },
                new Vert // Top-right
                {
                    Position = b,
                    AaEdgePosition = b + (Vector2.right * innerAaWidth),
                    EdgePosition = b + (Vector2.right * (innerAaWidth + edge)),
                    AaPosition = b + (Vector2.right * (innerAaWidth + edge + aaWidth)),
                },
            };

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
                        
                        var controlPoint = Bezier2(vert.Position, nextVert.Position, vert.ControlPosition, t);
                        var controlPointInnerEdge = Bezier2(vert.AaEdgePosition, nextVert.AaEdgePosition, vert.ControlAaEdgePosition, t);
                        var controlPointEdge = Bezier2(vert.EdgePosition, nextVert.EdgePosition, vert.ControlEdgePosition, t);
                        var controlPointAntiAliasing = Bezier2(vert.AaPosition, nextVert.AaPosition, vert.ControlAaPosition, t);

                        /*var mag1 = (controlPoint - controlPointInnerEdge).magnitude;
                        var mag2 = (controlPointInnerEdge - controlPointEdge).magnitude;
                        var mag3 = (controlPointEdge - controlPointAntiAliasing).magnitude;
                        
                        Debug.LogError($"(c - cEa).mag: {mag1}");
                        Debug.LogError($"(cEa - cE).mag: {mag2}");
                        Debug.LogError($"(cE - cAa).mag: {mag3}");
                        
                        var w = transform.TransformPoint(vert.ControlPosition);
                        var w1 = transform.TransformPoint(vert.ControlAaPosition);
                        var w2 = transform.TransformPoint(vert.ControlEdgePosition);
                        var w3 = transform.TransformPoint(vert.ControlAaEdgePosition);
                        
                        Debug.DrawRay(w, Vector2.up * 10, Color.red);
                        Debug.DrawRay(w1, Vector2.up * 10, Color.green);
                        Debug.DrawRay(w2, Vector2.up * 10, Color.blue);
                        Debug.DrawRay(w3, Vector2.up * 10, Color.yellow);*/
                        
                        var roundingVert = new Vert
                        {
                            Position = controlPoint,
                            AaEdgePosition = controlPointInnerEdge,
                            EdgePosition = controlPointEdge,
                            AaPosition = controlPointAntiAliasing,
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

        private Vector2 Bezier(Vector2 pointX, Vector2 pointY, Vector2 controlPoint, float t)
        {
            var a = Vector2.Lerp(pointX, controlPoint, t);
            var b = Vector2.Lerp(controlPoint, pointY, t);
            var c = Vector2.Lerp(a, b, t);
            
            return c;
        }
        
        private Vector2 Bezier2(Vector2 pointX, Vector2 pointY, Vector2 controlPoint, float t)
        {
            var baseDistance = (((pointX + pointY) / 2) - controlPoint).magnitude;
            var controlPointA = controlPoint + (Vector2.left * (baseDistance * 0.5f));
            var controlPointB = controlPoint + (Vector2.right * (baseDistance * 0.5f));
            
            /*var xca = Vector2.Lerp(pointX, controlPointA, t);
            var cacb = Vector2.Lerp(controlPointA, controlPointB, t);
            var cby = Vector2.Lerp(controlPointB, pointY, t);
            var u = Vector2.Lerp(xca, cacb, t);
            var v = Vector2.Lerp(cacb, cby, t);
            
            var result = Vector2.Lerp(u, v, t);*/
            
            return CubicBezier(pointX, controlPointA, pointY, controlPointB, t);
        }
        
        private Vector2 CubicBezier(Vector2 pointX, Vector2 tangentX, Vector2 pointY, Vector2 tangentY, float t)
        {
            var xtx = Vector2.Lerp(pointX, tangentX, t);
            var txty = Vector2.Lerp(tangentX, tangentY, t);
            var tyy = Vector2.Lerp(tangentY, pointY, t);
            
            var a = Vector2.Lerp(xtx, txty, t);
            var b = Vector2.Lerp(txty, tyy, t);
            
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
