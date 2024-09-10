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
            Fill = 0,
            Edge = 2,
            EdgeAndFill = 3,
        };

        private enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        };

        [FormerlySerializedAs("startAngle")]
        [SerializeField, Range(0.0f, 360.0f)]
        private int _startAngle = 0;
        
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
                _hullSections,
                _startAngle * Mathf.Deg2Rad,
                _isDashed,
                _antiAliasingLayerWidth,
                edge,
                _innerAntiAliasingLayerWidth); 

            vh.Clear();
            
            switch (_mode)
            {
                case Mode.Fill:
                {
                    GenerateFill(vh, in ellipseInfo);
                    break;
                }
                case Mode.Edge:
                {
                    GenerateEdge(vh, in ellipseInfo);
                    break;
                }
                case Mode.EdgeAndFill:
                {
                    GenerateEdgeAndFill(vh, in ellipseInfo);
                    break;
                }
            }
            
            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }
        
        private void GenerateFill(VertexHelper vh, in EllipseInfo info)
        {
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);

            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            vh.AddVert(
                centerPosition,
                CalculateColor(in centerPosition, in colorData),
                CalculateUv(in centerPosition, in info.EnclosingRect));
            
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.Fill);

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

        private void GenerateEdge(VertexHelper vh, in EllipseInfo info)
        {
            var centerPosition = info.EnclosingRect.center;
            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.Edge);
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);
            
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
        
        private void GenerateEdgeAndFill(VertexHelper vh, in EllipseInfo info)
        {
            var centerPosition = info.EnclosingRect.center;
            var colorData = new ColorCalculationData(info.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var centerColor = CalculateColor(in centerPosition, in colorData);
            var centerUv = CalculateUv(in centerPosition, in info.EnclosingRect);
            vh.AddVert(centerPosition, centerColor, centerUv);

            var hullSettings = new HullSettings(info.EnclosingRect, info.IsAaEnabled, Mode.EdgeAndFill);
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(info.HullSections, out SpanBuffer<Vert> buffer);
            
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
