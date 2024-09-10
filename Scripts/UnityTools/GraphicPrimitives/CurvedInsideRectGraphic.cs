using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Curved Inside Rect Graphic", 12)]
    public class CurvedInsideRectGraphic : Image
    {
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        
        private enum GradientType
        {
            Horizontal,
            Vertical,
            Radial,
        }
        
        private const float SmallDelta = 0.001f; 
        
        [SerializeField]
        private AnimationCurve _topCurveAmount = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f);
        
        [SerializeField]
        private bool _shouldUseGradient;
        
        [SerializeField]
        private GradientType _gradientType;
        
        [SerializeField]
        private Gradient _gradientColor = new();
        
        [SerializeField, Range(3, 100)]
        private int _curveQuality = 10;
        
        [SerializeField, Range(0, 100)]
        private float _antiAliasingWidth = 0;
        
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

        public (Vector3 position, Vector3 normal) GetWorldPositionAndNormalOnCurve(float probeValue)
        {
            var position = GetLocalPositionFromRelativePosition(probeValue);
            
            var slightlyLefterRelativePosition = probeValue - 0.001f;
            slightlyLefterRelativePosition = slightlyLefterRelativePosition < 0f ? 0f : slightlyLefterRelativePosition;
            
            var slightlyRighterRelativePosition = probeValue + 0.001f;
            slightlyRighterRelativePosition = slightlyRighterRelativePosition > 1f ? 1f : slightlyRighterRelativePosition;
            
            var slightlyLefterPosition = GetLocalPositionFromRelativePosition(slightlyLefterRelativePosition);
            var slightlyRighterPosition = GetLocalPositionFromRelativePosition(slightlyRighterRelativePosition);

            var delta = (slightlyRighterPosition - slightlyLefterPosition).normalized;
            var normal = Vector2.Perpendicular(delta);

            var worldPoint = transform.TransformPoint(position);
            var worldNormal = transform.TransformDirection(normal);
            
            return (worldPoint, worldNormal);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            
            using var memoryOwner = MemoryPool<UIVertex>.Shared.Rent(_curveQuality * 10);
            var hullVertices = memoryOwner.Memory.Slice(0, _curveQuality * 10).Span;
            var hullVertCount = 0;

            var rect = GetPixelAdjustedRect();
            var halfWidth = rect.width / 2;
            
            hullVertices[hullVertCount++] = AddVert(in vh, in rect, new Vector2(rect.xMin, rect.yMin), new Vector3(-0.5f, -0.5f).normalized);
            hullVertices[hullVertCount++] = AddVert(in vh, in rect, new Vector2(rect.xMax, rect.yMin), new Vector3(0.5f, -0.5f).normalized);
            hullVertices[hullVertCount++] = AddVert(in vh, in rect, new Vector2(rect.center.x, rect.yMin), new Vector3(0.0f, -1.0f).normalized);
            
            var verticesOnCurve = _curveQuality * 2;
            var halfQuality = _curveQuality;
            var step = 1.0f / (_curveQuality - 1);
            
            for (int i = 0; i < verticesOnCurve; i++)
            {
                if (i == halfQuality)
                {
                    continue;
                }
                
                Vector2 localPosition;
                float probePointValue = 0;
                var isInversed = i > halfQuality;
                
                if (!isInversed)
                {
                    probePointValue = i * step;
                    var x = rect.xMin + (probePointValue * halfWidth);
                    var y = rect.yMin + (_topCurveAmount.Evaluate(probePointValue) * rect.height);

                    localPosition = new Vector2(x, y);
                }
                else
                {
                    var halfIndex = i % halfQuality;
                    var reversedHalfIndex = halfQuality - halfIndex - 1;
                    probePointValue = reversedHalfIndex * step;
                    var x = rect.center.x + (halfIndex * step * halfWidth);
                    var y = rect.yMin + (_topCurveAmount.Evaluate(probePointValue) * rect.height);
                    
                    localPosition = new Vector2(x, y);
                }

                Vector3? additionalNormal = default;
                if (i == 0)
                {
                    additionalNormal = new Vector2(-0.5f, 0.5f).normalized;
                }
                else if (i == verticesOnCurve - 1)
                {
                    additionalNormal = new Vector2(0.5f, 0.5f).normalized;
                }
                
                hullVertices[hullVertCount++] = AddVert(in vh, in rect, localPosition, CalculateNormal(probePointValue, isInversed), in additionalNormal);
            }

            if (sprite != null && sprite.packed)
            {
                for (int i = 0; i < hullVertCount; i++)
                {
                    ref var vert = ref hullVertices[i];
                    
                    var x = Mathf.Lerp(sprite.uv[0].x, sprite.uv[1].x, vert.uv0.x);
                    var y = Mathf.Lerp(sprite.uv[0].y, sprite.uv[1].y, vert.uv0.y);

                    vert.uv0 = new Vector2(x, y);
                    vh.SetUIVertex(vert, i);
                }
            }

            var vertCount = vh.currentVertCount;
            var nonCurvePointsCount = 3;
            var basePoint = nonCurvePointsCount + 1;
            
            for (int i = basePoint; i  < _curveQuality + nonCurvePointsCount; i++)
            {
                var vertexIndex = i;
                var prevVertexIndex = i - 1;
            
                vh.AddTriangle(0, prevVertexIndex, vertexIndex);
            }

            var centerPointIndex = _curveQuality + nonCurvePointsCount - 1;
            
            vh.AddTriangle(0, centerPointIndex, 2);
            vh.AddTriangle(1, 2, centerPointIndex);

            for (int i = centerPointIndex + 1; i < vertCount; i++)
            {
                var vertexIndex = i;
                var prevVertexIndex = i - 1;
            
                vh.AddTriangle(1, prevVertexIndex, vertexIndex);
            }

            var aaHullStartIndex = hullVertCount;
            
            {
                var originalVertex = hullVertices[hullVertCount - 1];
                var vert = GetDisplacedTransparentVertex(in originalVertex, originalVertex.uv1);
                
                vh.AddVert(vert);
            }
            
            {
                var originalVertex = hullVertices[1];
                var vert = GetDisplacedTransparentVertex(in originalVertex, originalVertex.normal);
                
                vh.AddVert(vert);
            }
            
            {
                var originalVertex = hullVertices[0];
                var vert = GetDisplacedTransparentVertex(in originalVertex, originalVertex.normal);
                
                vh.AddVert(vert);
            }
            
            {
                var originalVertex = hullVertices[3];
                var vert = GetDisplacedTransparentVertex(in originalVertex, originalVertex.uv1);
                
                vh.AddVert(vert);
            }
                        
            vh.AddTriangle(hullVertCount - 1, aaHullStartIndex, aaHullStartIndex + 1);
            vh.AddTriangle(hullVertCount - 1, aaHullStartIndex + 1, 1);
            
            vh.AddTriangle(1, aaHullStartIndex + 1, aaHullStartIndex + 2);
            vh.AddTriangle(1, aaHullStartIndex + 2, 0);
            
            vh.AddTriangle(0, aaHullStartIndex + 2, aaHullStartIndex + 3);
            vh.AddTriangle(0, aaHullStartIndex + 3, 3);

            for (int i = 3; i < hullVertCount; i++)
            {
                ref UIVertex hullVertex = ref hullVertices[i];

                Vector2 aaPointPosition = hullVertex.position + (hullVertex.normal * _antiAliasingWidth);
                var aaVertex = hullVertex;
                aaVertex.position = aaPointPosition;
                aaVertex.color = aaVertex.color.WithAlpha(0);

                var aaHullIndex = vh.currentVertCount;
                
                vh.AddVert(aaVertex);

                if (i == 3)
                {
                    continue;
                }
                
                vh.AddTriangle(i - 1, aaHullIndex - 1, aaHullIndex);
                vh.AddTriangle(i - 1, aaHullIndex, i);
            }
            
            vh.AddTriangle(aaHullStartIndex + 3, hullVertCount + 4, 3);
            vh.AddTriangle(vh.currentVertCount - 1, aaHullStartIndex, hullVertCount - 1);

        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
            ApplyStencil();
        }
#endif
        
        private UIVertex AddVert(in VertexHelper vh, in Rect rect, in Vector2 position, in Vector3 normal, in Vector3? additionalNormal = null)
        {
            UIVertex vertex = UIVertex.simpleVert;
            
            vertex.position = position;
            vertex.color = CalculateColor(in position, in rect);
            vertex.uv0 = CalculateUv(in position, in rect);
            vertex.normal = normal;

            if (additionalNormal.HasValue)
            {
                vertex.uv1 = additionalNormal.Value;
            }
            
            vh.AddVert(vertex);
            
            return vertex;
        }
        
        private Vector2 GetLocalPositionFromRelativePosition(float probeValue)
        {
            var rect = GetPixelAdjustedRect();

            var x = rect.xMin + (rect.width * probeValue);
            var actualProbeValue = probeValue < 0.5f ? probeValue / 0.5f : 1 - ((probeValue - 0.5f) / 0.5f);
            var probedValue = _topCurveAmount.Evaluate(actualProbeValue);
            var y = rect.yMin + (probedValue * rect.height);

            return new Vector2(x, y);
        }
        
        private UIVertex GetDisplacedTransparentVertex(in UIVertex originalVertex, Vector2 displacementDirection)
        {
            var displacedPosition = (Vector2)originalVertex.position + (displacementDirection * _antiAliasingWidth);
            var resultVertex = originalVertex;
            resultVertex.position = displacedPosition;
            resultVertex.color = resultVertex.color.WithAlpha(0);
            resultVertex.uv1 = Vector4.zero;

            return resultVertex;
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
        
        private Color32 CalculateColor(in Vector2 vertexPosition, in Rect enclosingRect)
        {
            if (!_shouldUseGradient)
            {
                return color;
            }

            if (enclosingRect.width == 0 || enclosingRect.height == 0)
            {
                return _gradientColor.Evaluate(0);
            }
            
            switch (_gradientType)
            {
                case GradientType.Horizontal:
                {
                    var t = (vertexPosition.x - enclosingRect.xMin) / enclosingRect.width;
                    return _gradientColor.Evaluate(t);
                }
                case GradientType.Vertical:
                {
                    var t = (vertexPosition.y - enclosingRect.yMin) / enclosingRect.height;
                    return _gradientColor.Evaluate(t);
                }
                case GradientType.Radial:
                {
                    var halfWidth = enclosingRect.width / 2;
                    var centerPosition = new Vector2(enclosingRect.xMin + halfWidth, enclosingRect.yMax);
                    
                    if (vertexPosition == centerPosition)
                    {
                        return _gradientColor.Evaluate(0.0f);
                    }
                    else
                    {
                        return _gradientColor.Evaluate(1.0f);
                    }
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(_gradientType), _gradientType, "Unknown gradient type used.");
            }
        }

        private Vector4 CalculateUv(in Vector2 vertexPosition, in Rect enclosingRect)
        {
            var u = (vertexPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (vertexPosition.y - enclosingRect.yMin) / enclosingRect.height;
            
            return new Vector2(u, v);
        }
    }
}
