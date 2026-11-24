using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Curved Rect Graphic", 12)]
    public class CurvedRectGraphic : PrimitiveImage
    {
        [SerializeField] private AnimationCurve _topCurveAmount = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField, Range(3, 100)] private int _curveQuality = 10;
        [SerializeField] private ClosedShapeMode _mode = ClosedShapeMode.EdgeAndFill;
        [SerializeField] private bool _shouldUseGradient;
        [SerializeField] private ContourGradient _gradientType;
        [SerializeField] private Gradient _gradientColor = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _edgeThickness = 1f;
        [SerializeField] private Color _edgeColor = Color.white;
        [SerializeField] private bool _shouldUseEdgeGradient;
        [SerializeField] private ContourGradient _edgeGradientType;
        [SerializeField] private Gradient _edgeGradient = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _antiAliasingLayerWidth;
        [SerializeField, Min(0)] private float _innerAntiAliasingLayerWidth;

        public ClosedShapeMode Mode
        {
            get { return _mode; }
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    SetVerticesDirty();
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            int quality = Mathf.Clamp(_curveQuality, 3, 100);
            Span<Vector2> contour = stackalloc Vector2[(quality - 1) * 2 + 3];
            int count = ClosedContour.BuildSymmetricCurve(_topCurveAmount, rect, quality, true, contour);
            var fill = new ContourPaint(color, _shouldUseGradient ? _gradientColor : null, _gradientType);
            var edge = new ContourPaint(_edgeColor, _shouldUseEdgeGradient ? _edgeGradient : null, _edgeGradientType);
            ClosedContour.Draw(mesh, contour[..count], rect, GraphicMesh.GetSpriteUv(overrideSprite), _mode, _edgeThickness,
                _antiAliasingLayerWidth, _innerAntiAliasingLayerWidth, fill, edge);
        }
    }
}
