using System;
using System.Buffers;
using JetBrains.Annotations;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Rounded Rect Graphic", 12)]
    public class RoundedRectGraphic : Image
    {
        private enum GradientType
        {
            Horizontal,
            Vertical,
        }
        
        internal enum EdgeType
        {
            None,
            OnlyEdge,
            EdgeAndFill,
        }
        
        internal enum EdgeThicknessType
        {
            Absolute,
            Relative,
        }
        
        [Serializable]
        private struct CornersRoundings
        {
            [Range(0, 0.5f)]
            public float BottomLeft;
            [Range(0, 0.5f)]
            public float TopLeft;
            [Range(0, 0.5f)]
            public float TopRight;
            [Range(0, 0.5f)]
            public float BottomRight;

            public CornersRoundings(float uniformRounding)
            {
                BottomLeft = uniformRounding;
                TopLeft = uniformRounding;
                TopRight = uniformRounding;
                BottomRight = uniformRounding;
            }
        }
        
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
            public Vector4 EdgePercent;
            public float Radius;
            public bool IsRoundingVert;
        }

        [SerializeField] private bool _shouldUseGradient;

        [SerializeField] private Gradient _colorGradient = new Gradient();

        [SerializeField] private GradientType _gradientType;

        [SerializeField, Range(0, 0.5f)] private float _cornerRadius = 0.25f;

        [SerializeField, Range(3, 36)] private int _cornerQuality = 10;
        
        [SerializeField]
        private bool _keepSquare = false;
        
        [SerializeField]
        private bool _separateRoundingForCorners = false;
        
        [SerializeField]
        private CornersRoundings _cornersRoundings;

        [SerializeField]
        private EdgeType _edgeType;
        
        [SerializeField]
        private EdgeThicknessType _edgeThicknessType;
        
        [SerializeField, Range(0, 100)]
        private float _edgeThickness = 0;
        
        [SerializeField, Range(0, 1)]
        private float _edgeThicknessRelative = 0;
        
        [SerializeField] private Color _edgeColor;
        
        [SerializeField] private bool _shouldUseGradientForEdge;

        [SerializeField] private Gradient _edgeColorGradient = new Gradient();

        [SerializeField] private GradientType _edgeGradientType;
        
        [SerializeField, Range(0, 100)]
        [Tooltip("Thickness of the line where edge color interpolates into mainColor")]
        private float _innerAaThickness = 0;
        
        [SerializeField, Range(0, 100)]
        [Tooltip("When greater than 0 adds thin mesh edge around generated mesh with alpha transitioning from 1 to 0 so it'll look smoother.")]
        private float _antiAliasingBorderThickness = 0;

        [SerializeField]
        private bool _isDashed;
        
        [SerializeField, Range(0, 1000)]
        private float _dashLengthPixels = 0;
        
        [SerializeField, Range(0, 1000)]
        private float _dashGapLengthPixels = 0;
        
        [SerializeField, Range(0, 1)]
        private float _dashShift = 0;
        
        [SerializeField, UsedImplicitly]
        private int _vertCount;
        
        [SerializeField, UsedImplicitly]
        private int _trianglesCount;

        private Material _ownMaterialCopy;

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
        
        public void SetColors(Color mainColor, Color edgeColor)
        {
            color = mainColor;
            _edgeColor = edgeColor;
            
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            
            _ownMaterialCopy = material;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect rect = GetPixelAdjustedRect();
            vh.Clear();

            _vertCount = 0;
            _trianglesCount = 0;
            
            var smallerSide = Math.Min(rect.width, rect.height);
            if (smallerSide <= 0.0f)
            {
                return;
            }
            
            if (_keepSquare)
            {
                var half = smallerSide * 0.5f;
                rect = new Rect(rect.center - new Vector2(half, half), new Vector2(smallerSide, smallerSide));
            }

            var isAaEnabled = _antiAliasingBorderThickness > 0.0f;
            var edgeType = _edgeType;
            
            if (IsNoRounding())
            {
                DrawSimpleRect(in vh, in rect, smallerSide, isAaEnabled, edgeType);
            }
            else
            {
                DrawRoundedRect(in vh, in rect, smallerSide, isAaEnabled, edgeType);
            }
            
            _vertCount = vh.currentVertCount;
            _trianglesCount = vh.currentIndexCount / 3;
        }

        private void DrawRoundedRect(in VertexHelper vh, in Rect rect, float smallerSide, bool isAaEnabled, EdgeType edgeType)
        {
            var cornersRoundings = _separateRoundingForCorners
                ? _cornersRoundings
                : new CornersRoundings(_cornerRadius);

            var cornersRadius = new CornersRoundings
            {
                TopLeft = (cornersRoundings.TopLeft - 0.00001f) * smallerSide,
                TopRight = (cornersRoundings.TopRight - 0.00001f) * smallerSide,
                BottomRight = (cornersRoundings.BottomRight - 0.00001f) * smallerSide,
                BottomLeft = (cornersRoundings.BottomLeft - 0.00001f) * smallerSide,
            };

            var roundedCornersCount = CalculateRoundedCorners(in cornersRoundings);

            var aa = _antiAliasingBorderThickness;
            var innerAa = _innerAaThickness;
            var edge = _edgeThicknessType switch
            {
                EdgeThicknessType.Absolute => _edgeThickness,
                EdgeThicknessType.Relative => _edgeThicknessRelative * smallerSide,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(EdgeThicknessType)}: {_edgeThicknessType}")
            };
            
            Span<Vert> cornersVerts = stackalloc Vert[]
            {
                new Vert // Bottom-left-left
                {
                    Position = new Vector2(rect.xMin, rect.yMin + cornersRadius.BottomLeft),
                    AaPosition = new Vector2(rect.xMin - aa, rect.yMin + cornersRadius.BottomLeft),
                    EdgePosition = new Vector2(rect.xMin + edge, rect.yMin + cornersRadius.BottomLeft),
                    AaEdgePosition = new Vector2(rect.xMin + edge + innerAa, rect.yMin + cornersRadius.BottomLeft),
                    IsRoundingVert = false,
                    /*EdgePercent = new (
                        0.0f,
                        -aa / perimeterHalf,
                        edge / perimeterHalf,
                        (edge + innerAa) / perimeterHalf),*/
                },
                new Vert // Top-left-left
                {
                    Position = new Vector2(rect.xMin, rect.yMax - cornersRadius.TopLeft),
                    AaPosition = new Vector2(rect.xMin - aa, rect.yMax - cornersRadius.TopLeft),
                    EdgePosition = new Vector2(rect.xMin + edge, rect.yMax - cornersRadius.TopLeft),
                    AaEdgePosition = new Vector2(rect.xMin + edge + innerAa, rect.yMax - cornersRadius.TopLeft),
                    IsRoundingVert = !Mathf.Approximately(cornersRoundings.TopLeft, 0.0f),
                    Radius = cornersRadius.TopLeft,
                },
                new Vert // Top-top-left
                {
                    Position = new Vector2(rect.xMin + cornersRadius.TopLeft, rect.yMax),
                    AaPosition = new Vector2(rect.xMin + cornersRadius.TopLeft, rect.yMax + aa),
                    EdgePosition = new Vector2(rect.xMin + cornersRadius.TopLeft, rect.yMax - edge),
                    AaEdgePosition = new Vector2(rect.xMin + cornersRadius.TopLeft, rect.yMax - edge - innerAa),
                    IsRoundingVert = false,
                },
                new Vert // Top-top-right
                {
                    Position = new Vector2(rect.xMax - cornersRadius.TopRight, rect.yMax),
                    AaPosition = new Vector2(rect.xMax - cornersRadius.TopRight, rect.yMax + aa),
                    EdgePosition = new Vector2(rect.xMax - cornersRadius.TopRight, rect.yMax - edge),
                    AaEdgePosition = new Vector2(rect.xMax - cornersRadius.TopRight, rect.yMax - edge - innerAa),
                    IsRoundingVert = !Mathf.Approximately(cornersRoundings.TopRight, 0.0f),
                    Radius = cornersRadius.TopRight,
                },
                new Vert // Top-right-right
                {
                    Position = new Vector2(rect.xMax, rect.yMax - cornersRadius.TopRight),
                    AaPosition = new Vector2(rect.xMax + aa, rect.yMax - cornersRadius.TopRight),
                    EdgePosition = new Vector2(rect.xMax - edge, rect.yMax - cornersRadius.TopRight),
                    AaEdgePosition = new Vector2(rect.xMax - edge - innerAa, rect.yMax - cornersRadius.TopRight),
                    IsRoundingVert = false,
                },
                new Vert // Bottom-right-right
                {
                    Position = new Vector2(rect.xMax, rect.yMin + cornersRadius.BottomRight),
                    AaPosition = new Vector2(rect.xMax + aa, rect.yMin + cornersRadius.BottomRight),
                    EdgePosition = new Vector2(rect.xMax - edge, rect.yMin + cornersRadius.BottomRight),
                    AaEdgePosition = new Vector2(rect.xMax - edge - innerAa, rect.yMin + cornersRadius.BottomRight),
                    IsRoundingVert = !Mathf.Approximately(cornersRoundings.BottomRight, 0.0f),
                    Radius = cornersRadius.BottomRight,
                },
                new Vert // Bottom-bottom-right
                {
                    Position = new Vector2(rect.xMax - cornersRadius.BottomRight, rect.yMin),
                    AaPosition = new Vector2(rect.xMax - cornersRadius.BottomRight, rect.yMin - aa),
                    EdgePosition = new Vector2(rect.xMax - cornersRadius.BottomRight, rect.yMin + edge),
                    AaEdgePosition = new Vector2(rect.xMax - cornersRadius.BottomRight, rect.yMin + edge + innerAa),
                    IsRoundingVert = false,
                },
                new Vert // Bottom-bottom-left
                {
                    Position = new Vector2(rect.xMin + cornersRadius.BottomLeft, rect.yMin),
                    AaPosition = new Vector2(rect.xMin + cornersRadius.BottomLeft, rect.yMin - aa),
                    EdgePosition = new Vector2(rect.xMin + cornersRadius.BottomLeft, rect.yMin + edge),
                    AaEdgePosition = new Vector2(rect.xMin + cornersRadius.BottomLeft, rect.yMin + edge + innerAa),
                    IsRoundingVert = !Mathf.Approximately(cornersRoundings.BottomLeft, 0.0f),
                    Radius = cornersRadius.BottomLeft,
                },
            };

            var maxVerts = cornersVerts.Length + (_cornerQuality * roundedCornersCount);
            using var memoryOwner = MemoryPool<Vert>.Shared.Rent(maxVerts);
            Span<Vert> verts = memoryOwner.Memory.Slice(0, maxVerts).Span;
            var buffer = new SpanBuffer<Vert>(verts, 0);

            var center = new Vector2(rect.center.x, rect.center.y);
            var calculatedColor = CalculateColor(in center, in rect, _shouldUseGradient, color, _gradientType, _colorGradient);
            var calculatedUv = CalculateUv(in center, in rect);
            vh.AddVert(center, calculatedColor, calculatedUv);

            for (int i = 0; i < cornersVerts.Length; i++)
            {
                ref var cornerVert = ref cornersVerts[i];

                AddHullVert(in vh, ref cornerVert, in rect, isAaEnabled, edgeType, ref buffer);

                if (cornerVert.IsRoundingVert)
                {
                    var radius = cornerVert.Radius;
                    ref var prevCornerVert = ref cornersVerts[i - 1];

                    var direction = (cornerVert.Position - prevCornerVert.Position).normalized;
                    var rightNormal = new Vector2(direction.y, -direction.x);
                    var roundingCenter = cornerVert.Position + (rightNormal * radius);

                    for (int j = 1; j <= _cornerQuality; j++)
                    {
                        float angle = (j / (float)_cornerQuality) * 90;

                        var vert = new Vert
                        {
                            Position = RotatePoint(cornerVert.Position, roundingCenter, angle),
                            AaPosition = isAaEnabled ? RotatePoint(cornerVert.AaPosition, roundingCenter, angle) : Vector2.zero,
                            EdgePosition = edgeType != EdgeType.None ? RotatePoint(cornerVert.EdgePosition, roundingCenter, angle) : Vector2.zero,
                            AaEdgePosition = edgeType != EdgeType.None ? RotatePoint(cornerVert.AaEdgePosition, roundingCenter, angle) : Vector2.zero,
                        };

                        AddHullVert(in vh, ref vert, in rect, isAaEnabled, edgeType, ref buffer);
                    }
                }
            }

            var filledVerts = buffer.FilledBufferSlice();

            for (int i = 1; i < filledVerts.Length; i++)
            {
                var (vert, prevVert) = filledVerts.GetCircularConsecutivePair(i);

                if (edgeType == EdgeType.None)
                {
                    vh.AddTriangle(0, vert.Index, prevVert.Index);
                }
                else
                {
                    if (edgeType == EdgeType.EdgeAndFill)
                    {
                        vh.AddTriangle(0, vert.AaEdgeIndex, prevVert.AaEdgeIndex);
                    }

                    vh.AddTriangle(vert.AaEdgeIndex, vert.EdgeIndex, prevVert.EdgeIndex);
                    vh.AddTriangle(vert.AaEdgeIndex, prevVert.AaEdgeIndex, prevVert.EdgeIndex);

                    vh.AddTriangle(vert.EdgeIndex, vert.Index, prevVert.Index);
                    vh.AddTriangle(vert.EdgeIndex, prevVert.EdgeIndex, prevVert.Index);
                }

                if (isAaEnabled)
                {
                    vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.AaIndex);
                    vh.AddTriangle(prevVert.Index, vert.AaIndex, vert.Index);
                }
            }

            if (!cornersVerts[^1].IsRoundingVert && isAaEnabled)
            {
                vh.AddTriangle(verts[^1].Index, verts[^1].AaIndex, verts[0].AaIndex);
            }
        }

        private void DrawSimpleRect(in VertexHelper vh, in Rect rect, float smallerSide, bool isAaEnabled, EdgeType edgeType)
        {
            var aa = _antiAliasingBorderThickness;
            var innerAa = _innerAaThickness;
            var edge = _edgeThicknessType switch
            {
                EdgeThicknessType.Absolute => _edgeThickness,
                EdgeThicknessType.Relative => _edgeThicknessRelative * smallerSide,
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(EdgeThicknessType)}: {_edgeThicknessType}")
            };
            
            var perimeterHalf = rect.width + rect.height;
            var perimeter = 2 * perimeterHalf;
            var h = rect.height;
            var he = rect.height - (2 * edge);
            
            Span<Vert> corners = stackalloc Vert[]
            {
                new Vert // Bottom-left
                {
                    Position = new Vector2(rect.xMin, rect.yMin),
                    AaPosition = new Vector2(rect.xMin - aa, rect.yMin - aa),
                    EdgePosition = new Vector2(rect.xMin + edge, rect.yMin + edge),
                    AaEdgePosition = new Vector2(rect.xMin + edge + innerAa, rect.yMin + edge + innerAa),
                    EdgePercent = new (
                        0.0f,
                        -aa / perimeterHalf,
                        edge / perimeterHalf,
                        (edge + innerAa) / perimeterHalf),
                },
                new Vert // Top-left
                {
                    Position = new Vector2(rect.xMin, rect.yMax),
                    AaPosition = new Vector2(rect.xMin - aa, rect.yMax + aa),
                    EdgePosition = new Vector2(rect.xMin + edge, rect.yMax - edge),
                    AaEdgePosition = new Vector2(rect.xMin + edge + innerAa, rect.yMax - edge - innerAa),
                    EdgePercent = new (
                        h / perimeterHalf,
                        (h + aa) / perimeterHalf,
                        (edge + he) / perimeterHalf,
                        (h - edge - innerAa) / perimeterHalf),
                },
                new Vert // Top-right
                {
                    Position = new Vector2(rect.xMax, rect.yMax),
                    AaPosition = new Vector2(rect.xMax + aa, rect.yMax + aa),
                    EdgePosition = new Vector2(rect.xMax - edge, rect.yMax - edge),
                    AaEdgePosition = new Vector2(rect.xMax - edge - innerAa, rect.yMax - edge - innerAa),
                    EdgePercent = new (
                        1.0f,
                        (perimeterHalf + aa) / perimeterHalf,
                        1.0f - (edge / perimeterHalf),
                        (perimeterHalf - edge - innerAa) / perimeterHalf),
                },
                new Vert // Bottom-right
                {
                    Position = new Vector2(rect.xMax, rect.yMin),
                    AaPosition = new Vector2(rect.xMax + aa, rect.yMin - aa),
                    EdgePosition = new Vector2(rect.xMax - edge, rect.yMin + edge),
                    AaEdgePosition = new Vector2(rect.xMax - edge - innerAa, rect.yMin + edge + innerAa),
                    EdgePercent = new (
                        1 - (h / perimeterHalf),
                        1 - (h - aa) / perimeterHalf,
                        1 - ((edge + he) / perimeterHalf),
                        1 - (h + edge + innerAa) / perimeterHalf),
                },
            };
            
            Span<Vert> verts = stackalloc Vert[4];
            var buffer = new SpanBuffer<Vert>(verts, 0);

            // var center = new Vector2(rect.center.x, rect.center.y);
            // var calculatedColor = CalculateColor(in center, in rect, _shouldUseGradient, color, _gradientType, _colorGradient);
            // var calculatedUv = CalculateUv(in center, in rect);
            // var uv1 = new Vector4(1.0f, 1.0f);
            // var defaultVert = UIVertex.simpleVert;
            // vh.AddVert(center, calculatedColor, calculatedUv, uv1, defaultVert.normal, defaultVert.tangent);
            
            for (int i = 0; i < corners.Length; i++)
            {
                ref var cornerPosition = ref corners[i];

                AddHullVert(in vh, ref cornerPosition, in rect, isAaEnabled, edgeType, ref buffer);
            }

            if (edgeType == EdgeType.None)
            {
                vh.AddTriangle(verts[0].Index, verts[1].Index, verts[2].Index);
                vh.AddTriangle(verts[0].Index, verts[2].Index, verts[3].Index);
            }
            else
            {
                for (int i = 1; i <= verts.Length; i++)
                {
                    var (vert, prevVert) = verts.GetCircularConsecutivePair(i);
                
                    vh.AddTriangle(vert.AaEdgeIndex, vert.EdgeIndex, prevVert.EdgeIndex);
                    vh.AddTriangle(vert.AaEdgeIndex, prevVert.AaEdgeIndex, prevVert.EdgeIndex);
                
                    vh.AddTriangle(vert.EdgeIndex, vert.Index, prevVert.Index);
                    vh.AddTriangle(vert.EdgeIndex, prevVert.EdgeIndex, prevVert.Index);
                }
                
                if (edgeType == EdgeType.EdgeAndFill)
                {
                    vh.AddTriangle(verts[0].AaEdgeIndex, verts[1].AaEdgeIndex, verts[2].AaEdgeIndex);
                    vh.AddTriangle(verts[0].AaEdgeIndex, verts[2].AaEdgeIndex, verts[3].AaEdgeIndex);
                }
            }

            if (isAaEnabled)
            {
                for (int i = 1; i <= verts.Length; i++)
                {
                    var (vert, prevVert) = verts.GetCircularConsecutivePair(i);

                    vh.AddTriangle(prevVert.Index, prevVert.AaIndex, vert.AaIndex);
                    vh.AddTriangle(prevVert.Index, vert.AaIndex, vert.Index);
                }
            }

            /*if (_isDashed)
            {
                if (_ownMaterialCopy == null)
                {
                    _ownMaterialCopy = material;
                }

                _ownMaterialCopy.SetFloat("_Perimeter", perimeter);
                _ownMaterialCopy.SetFloat("_DashLengthPixels", _dashLengthPixels);
                _ownMaterialCopy.SetFloat("_DashGapLengthPixels", _dashGapLengthPixels);
                _ownMaterialCopy.SetFloat("_Shift", _dashShift);
            }*/
        }

        private int CalculateRoundedCorners(in CornersRoundings cornersRoundings)
        {
            var result = 0;
            if (!Mathf.Approximately(cornersRoundings.BottomLeft, 0.0f))
            {
                result++;
            }
            if (!Mathf.Approximately(cornersRoundings.TopLeft, 0.0f))
            {
                result++;
            }
            if (!Mathf.Approximately(cornersRoundings.TopRight, 0.0f))
            {
                result++;
            }
            if (!Mathf.Approximately(cornersRoundings.BottomRight, 0.0f))
            {
                result++;
            }

            return result;
        }

        private bool IsNoRounding()
        {
            if (!_separateRoundingForCorners)
            {
                return Mathf.Approximately(_cornerRadius, 0.0f);
            }
            else
            {
                return Mathf.Approximately(_cornersRoundings.BottomLeft, 0.0f)
                       && Mathf.Approximately(_cornersRoundings.TopLeft, 0.0f)
                       && Mathf.Approximately(_cornersRoundings.TopRight, 0.0f)
                       && Mathf.Approximately(_cornersRoundings.BottomRight, 0.0f);
            }
        }

#if UNITY_EDITOR
        // If the component changes in the inspector, update the mesh
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif

        private void AddHullVert(in VertexHelper vh, ref Vert vertex, in Rect rect, bool isAaEnabled, EdgeType edgeType, ref SpanBuffer<Vert> verts)
        {
            if (edgeType != EdgeType.None)
            {
                var hullVertexUv = CalculateUv(in vertex.Position, in rect);
                var edgeVertexUv = CalculateUv(in vertex.EdgePosition, in rect);
                var edgeAaVertexUv = CalculateUv(in vertex.AaEdgePosition, in rect);
                var edgeColor = CalculateColor(in vertex.Position, in rect, _shouldUseGradientForEdge, _edgeColor, _edgeGradientType, _edgeColorGradient);
                var mainColor = CalculateColor(in vertex.Position, in rect, _shouldUseGradient, color, _gradientType, _colorGradient);

                var defaultVert = UIVertex.simpleVert;
                
                vertex.Index = (short)vh.currentVertCount;
                vh.AddVert(vertex.Position, edgeColor, hullVertexUv, new Vector2(vertex.EdgePercent.x, 0.0f), defaultVert.normal, defaultVert.tangent);
                
                vertex.EdgeIndex = (short)vh.currentVertCount;
                vh.AddVert(vertex.EdgePosition, edgeColor, edgeVertexUv, new Vector2(vertex.EdgePercent.z, 0.0f), defaultVert.normal, defaultVert.tangent);

                vertex.AaEdgeIndex = (short)vh.currentVertCount;
                vh.AddVert(vertex.AaEdgePosition, mainColor, edgeAaVertexUv, new Vector4(vertex.EdgePercent.w, 1.0f), defaultVert.normal, defaultVert.tangent);
                
                if (isAaEnabled)
                {
                    vertex.AaIndex = (short)vh.currentVertCount;
                    vh.AddVert(vertex.AaPosition, edgeColor.WithAlpha(0.0f), hullVertexUv, new Vector2(vertex.EdgePercent.y, 0.0f), defaultVert.normal, defaultVert.tangent);
                }
            }
            else
            {
                var calculatedColor = CalculateColor(in vertex.Position, in rect, _shouldUseGradient, color, _gradientType, _colorGradient);
                var calculatedUv = CalculateUv(in vertex.Position, in rect);
            
                vertex.Index = (short)vh.currentVertCount;
                vh.AddVert(vertex.Position, calculatedColor, calculatedUv);

                if (isAaEnabled)
                {
                    vertex.AaIndex = (short)vh.currentVertCount;
                    vh.AddVert(vertex.AaPosition, calculatedColor.WithAlpha(0.0f), calculatedUv);
                }
            }
            
            verts.Add(vertex);
        }
        
        private static Vector2 RotatePoint(Vector2 pointToRotate, Vector2 centerPoint, float angle)
        {
            float angleInRadians = -angle * Mathf.Deg2Rad; // Convert angle to radians and make it clockwise
            float cos = Mathf.Cos(angleInRadians);
            float sin = Mathf.Sin(angleInRadians);

            Vector2 translatedPoint = new Vector2(pointToRotate.x - centerPoint.x, pointToRotate.y - centerPoint.y);

            Vector2 rotatedPoint = new Vector2(
                cos * translatedPoint.x - sin * translatedPoint.y,
                sin * translatedPoint.x + cos * translatedPoint.y);

            Vector2 finalPoint = new Vector2(rotatedPoint.x + centerPoint.x, rotatedPoint.y + centerPoint.y);

            return finalPoint;
        }

        private Color32 CalculateColor(in Vector2 cornerPosition, in Rect enclosingRect, bool shouldUseGradient, Color singleColor, GradientType gradientType, Gradient gradient)
        {
            if (!shouldUseGradient)
            {
                return singleColor;
            }

            switch (gradientType)
            {
                case GradientType.Horizontal:
                {
                    var t = (cornerPosition.x - enclosingRect.xMin) / enclosingRect.width;
                    var clampedT = Mathf.Clamp01(t);
                    
                    return gradient.Evaluate(clampedT);
                }
                case GradientType.Vertical:
                {
                    var t = (cornerPosition.y - enclosingRect.yMin) / enclosingRect.height;
                    var clampedT = Mathf.Clamp01(t);
                    
                    return gradient.Evaluate(clampedT);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(gradientType), gradientType, "Unknown gradient type used.");
            }
        }

        private Vector4 CalculateUv(in Vector2 cornerPosition, in Rect enclosingRect)
        {
            var u = (cornerPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (cornerPosition.y - enclosingRect.yMin) / enclosingRect.height;
            
            return new Vector2(u, v);
        }
    }
}
