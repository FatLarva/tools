using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Curved Inside Rect Graphic", 12)]
    public class CurvedInsideRectGraphic : PrimitiveImage
    {
        [SerializeField] private AnimationCurve _topCurveAmount = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [SerializeField, Range(3, 100)] private int _curveQuality = 10;
        [SerializeField] private ClosedShapeMode _mode = ClosedShapeMode.Fill;
        [SerializeField] private bool _shouldUseGradient;
        [SerializeField] private ContourGradient _gradientType;
        [SerializeField] private Gradient _gradientColor = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _edgeThickness = 1f;
        [SerializeField] private Color _edgeColor = Color.white;
        [SerializeField] private bool _shouldUseEdgeGradient;
        [SerializeField] private ContourGradient _edgeGradientType;
        [SerializeField] private Gradient _edgeGradient = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _antiAliasingWidth;
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

        public (Vector3 position, Vector3 normal) GetWorldPositionAndNormalOnCurve(float probeValue)
        {
            const float delta = 0.001f;
            Rect rect = GetPixelAdjustedRect();
            Vector2 position = ClosedContour.EvaluateSymmetricCurve(_topCurveAmount, rect, probeValue, false);
            Vector2 previous = ClosedContour.EvaluateSymmetricCurve(_topCurveAmount, rect, probeValue - delta, false);
            Vector2 next = ClosedContour.EvaluateSymmetricCurve(_topCurveAmount, rect, probeValue + delta, false);
            Vector2 normal = Vector2.Perpendicular((next - previous).normalized);
            return (transform.TransformPoint(position), transform.TransformDirection(normal));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            int quality = Mathf.Clamp(_curveQuality, 3, 100);
            Span<Vector2> contour = stackalloc Vector2[(quality - 1) * 2 + 3];
            int count = ClosedContour.BuildSymmetricCurve(_topCurveAmount, rect, quality, false, contour);
            var fill = new ContourPaint(color, _shouldUseGradient ? _gradientColor : null, _gradientType);
            var edge = new ContourPaint(_edgeColor, _shouldUseEdgeGradient ? _edgeGradient : null, _edgeGradientType);
            ClosedContour.Draw(mesh, contour[..count], rect, GraphicMesh.GetSpriteUv(overrideSprite), _mode, _edgeThickness,
                _antiAliasingWidth, _innerAntiAliasingLayerWidth, fill, edge);
        }
    }
}
