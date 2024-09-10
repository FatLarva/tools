using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Curved Rect Graphic", 12)]
    public class CurvedRectGraphic : Image
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
        
        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }
        
        private const float SmallDelta = 0.001f; 
        
        [SerializeField]
        private AnimationCurve _topCurveAmount = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f);
        
        [SerializeField, Range(3, 100)]
        private int _curveQuality = 10;
        
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
        
        private UvRect _lastUvRect;
        private Sprite _uvRectCalculatedForSprite;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var spriteUvs = RecalculateSpriteUvsIfNeeded(sprite, ref _uvRectCalculatedForSprite, ref _lastUvRect);
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(_curveQuality * 10, out SpanBuffer<Vert> buffer);

            var rect = GetPixelAdjustedRect();
            var halfWidth = rect.width / 2;

            var edge = _edgeThickness;
            var aaWidth = _antiAliasingLayerWidth;
            var innerAaWidth = _innerAntiAliasingLayerWidth;
            
            var aaEnabled = aaWidth > 0;
            var edgeEnabled = edge > 0;
            
            var hullSettings = new HullSettings(rect, aaEnabled, edgeEnabled);

            var centerPosition = new Vector2(rect.center.x, rect.yMax);
            var colorData = new ColorCalculationData(rect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in rect, in spriteUvs));

            var verticesOnCurve = _curveQuality * 2;
            var halfQuality = _curveQuality;
            var step = 1.0f / (_curveQuality - 1);

            for (int i = 0; i < verticesOnCurve; i++)
            {
                if (i == halfQuality)
                {
                    continue;
                }

                Vector2 position;
                Vector2 normal;
                if (i < halfQuality)
                {
                    var relativePosition = i * step;
                    var x = rect.xMin + (relativePosition * halfWidth);
                    var y = rect.yMin + (_topCurveAmount.Evaluate(relativePosition) * rect.height);
                    normal = CalculateNormal(relativePosition, false);

                    position = new Vector2(x, y);
                }
                else
                {
                    var halfIndex = i % halfQuality;
                    var reversedHalfIndex = halfQuality - halfIndex - 1;
                    var x = (halfIndex * step * halfWidth);
                    var y = rect.yMin + (_topCurveAmount.Evaluate(reversedHalfIndex * step) * rect.height);
                    normal = CalculateNormal(reversedHalfIndex * step, true);

                    position = new Vector2(x, y);
                }

                var vert = new Vert
                {
                    Position = position,
                    AaPosition = position - (normal * aaWidth),
                    EdgePosition = position + (normal * edge),
                    AaEdgePosition = position + (normal * (edge + innerAaWidth)),
                };

                if (edgeEnabled)
                {
                    AddHullVertWithEdge(in vh, ref vert, ref buffer, in hullSettings, in spriteUvs);
                }
                else
                {
                    AddHullVert(in vh, ref vert, ref buffer, in hullSettings, in spriteUvs);
                }
            }
            
            var filledVerts = buffer.FilledBufferSlice();

            for (int i = 1; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                if (edgeEnabled)
                {
                    vh.AddTriangle(0, vert.AaEdgeIndex, prevVert.AaEdgeIndex);

                    vh.AddTriangle(vert.EdgeIndex, vert.AaEdgeIndex, prevVert.AaEdgeIndex);
                    vh.AddTriangle(prevVert.EdgeIndex, prevVert.AaEdgeIndex, vert.EdgeIndex);
                    
                    vh.AddTriangle(vert.Index, vert.EdgeIndex, prevVert.EdgeIndex);
                    vh.AddTriangle(prevVert.Index, prevVert.EdgeIndex, vert.Index);
                }
                else
                {
                    vh.AddTriangle(0, vert.Index, prevVert.Index);
                }
                
                if (aaEnabled)
                {
                    vh.AddTriangle(vert.Index, vert.AaIndex, prevVert.AaIndex);
                    vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.Index);
                }
            }
        }

        private UvRect RecalculateSpriteUvsIfNeeded(Sprite sourceSprite, ref Sprite uvRectCalculatedForSprite, ref UvRect lastUvRect)
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
        }
        
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
            vh.AddVert(vertex.Position, CalculateColor(in vertex.Position, edgeColorData), CalculateUv(in vertex.Position, in settings.EnclosingRect, in uvRect));
            
            vertex.EdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.EdgePosition, CalculateColor(in vertex.EdgePosition, edgeColorData), CalculateUv(in vertex.EdgePosition, in settings.EnclosingRect, in uvRect));

            vertex.AaEdgeIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.AaEdgePosition, CalculateColor(in vertex.AaEdgePosition, mainColorData), CalculateUv(in vertex.AaEdgePosition, in settings.EnclosingRect, in uvRect));
            
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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
        
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
        
        private Vector2 CalculateNormal(float probePointValue, bool isInversed)
        {
            var slightlyLefterProbePoint = Mathf.Max(probePointValue - SmallDelta, 0.0f);
            var slightlyRighterProbePoint = Mathf.Min(probePointValue + SmallDelta, 1.0f);

            float slope;
            if (!isInversed)
            {
                slope = (_topCurveAmount.Evaluate(slightlyRighterProbePoint) - _topCurveAmount.Evaluate(slightlyLefterProbePoint)) / (2 * SmallDelta);
            }
            else
            {
                slope = (_topCurveAmount.Evaluate(slightlyLefterProbePoint) - _topCurveAmount.Evaluate(slightlyRighterProbePoint)) / (2 * SmallDelta);
            }
            
            Vector2 tangent = new Vector2(1, slope).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);

            return normal.normalized;
        }
    }
}
