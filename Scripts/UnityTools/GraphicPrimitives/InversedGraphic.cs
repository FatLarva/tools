using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    public abstract class InversedGraphic : PrimitiveImage
    {
        private enum EdgeThicknessMode
        {
            Absolute,
            Relative,
        }

        [SerializeField] private ClosedShapeMode _mode;
        [SerializeField] private Vector2 _holeCenter = new(0.5f, 0.5f);
        [SerializeField] private Vector2 _holeSize = new(0.65f, 0.65f);
        [SerializeField] private bool _shouldUseGradient;
        [SerializeField] private ContourGradient _gradientType;
        [SerializeField] private Gradient _gradientColor = ClosedContour.WhiteGradient();
        [SerializeField] private EdgeThicknessMode _edgeThicknessMode;
        [SerializeField, Min(0)] private float _edgeThickness = 1f;
        [SerializeField, Range(0f, 1f)] private float _edgeThicknessRelative;
        [SerializeField] private Color _edgeColor = Color.white;
        [SerializeField] private bool _shouldUseGradientForEdge;
        [SerializeField] private ContourGradient _edgeGradientType;
        [SerializeField] private Gradient _edgeColorGradient = ClosedContour.WhiteGradient();
        [SerializeField, Min(0)] private float _antiAliasingLayerWidth;
        [SerializeField, Min(0)] private float _innerAntiAliasingLayerWidth;
        [SerializeField, Tooltip("Allow UI behind the cutout to receive pointer events.")]
        private bool _raycastThroughHole;

        internal int VertexCount { get; private set; }
        internal int TriangleCount { get; private set; }
        internal bool MeshBudgetExceeded { get; private set; }
        internal bool LockHoleAspect => KeepAspect;

        private protected abstract bool IsEllipse { get; }
        private protected abstract bool KeepAspect { get; }
        private protected abstract int Detail { get; }

        public ClosedShapeMode DrawingMode
        {
            get { return _mode; }
            set { SetGeometry(ref _mode, value); }
        }

        /// <summary>Hole center relative to the panel. The visible center is clamped so the hole stays inside the panel.</summary>
        public Vector2 HoleCenter
        {
            get { return _holeCenter; }
            set { SetGeometry(ref _holeCenter, Clamp01(value)); }
        }

        /// <summary>Hole size relative to the panel. A zero dimension removes the hole.</summary>
        public Vector2 HoleSize
        {
            get { return _holeSize; }
            set { SetGeometry(ref _holeSize, Clamp01(value)); }
        }

        /// <summary>Whether pointer raycasts can reach UI behind the cutout.</summary>
        public bool RaycastThroughHole
        {
            get { return _raycastThroughHole; }
            set { _raycastThroughHole = value; }
        }

        public Color EdgeColor
        {
            get { return _edgeColor; }
            set { SetGeometry(ref _edgeColor, value); }
        }

        /// <summary>Stroke width in local UI units. Setting it selects absolute thickness.</summary>
        public float EdgeThicknessAbsolute
        {
            get { return _edgeThickness; }
            set { SetEdgeThickness(EdgeThicknessMode.Absolute, ref _edgeThickness, NonnegativeFinite(value)); }
        }

        /// <summary>Stroke width as a fraction of half the shorter side of the hole. Setting it selects relative thickness.</summary>
        public float EdgeThicknessRelative
        {
            get { return _edgeThicknessRelative; }
            set { SetEdgeThickness(EdgeThicknessMode.Relative, ref _edgeThicknessRelative, Mathf.Clamp01(NonnegativeFinite(value))); }
        }

        /// <summary>Width of the fade into the transparent hole, in local UI units.</summary>
        public float OuterAntiAliasing
        {
            get { return _antiAliasingLayerWidth; }
            set { SetGeometry(ref _antiAliasingLayerWidth, NonnegativeFinite(value)); }
        }

        /// <summary>Width of the stroke fade on the side facing the surrounding fill, in local UI units.</summary>
        public float InnerAntiAliasing
        {
            get { return _innerAntiAliasingLayerWidth; }
            set { SetGeometry(ref _innerAntiAliasingLayerWidth, NonnegativeFinite(value)); }
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            if (!base.Raycast(screenPoint, eventCamera))
            {
                return false;
            }

            if (!_raycastThroughHole ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var localPoint))
            {
                return true;
            }

            var panel = GetPixelAdjustedRect();
            var hole = GetHole(panel);
            return !InverseContour.ContainsHole(panel, hole, IsEllipse, GetCornerRadius(hole), Detail, localPoint);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            MeshBudgetExceeded = false;
            VertexCount = 0;
            TriangleCount = 0;

            var panel = GetPixelAdjustedRect();
            if (panel.width <= 0f || panel.height <= 0f)
            {
                return;
            }

            var hole = GetHole(panel);
            var thickness = _edgeThicknessMode == EdgeThicknessMode.Relative
                ? Mathf.Clamp01(NonnegativeFinite(_edgeThicknessRelative)) * Mathf.Min(hole.width, hole.height) * 0.5f
                : NonnegativeFinite(_edgeThickness);
            var fill = new ContourPaint(color, _shouldUseGradient ? _gradientColor : null, _gradientType);
            var edge = new ContourPaint(_edgeColor, _shouldUseGradientForEdge ? _edgeColorGradient : null, _edgeGradientType);
            MeshBudgetExceeded = !InverseContour.Draw(mesh, panel, hole, IsEllipse, GetCornerRadius(hole), Detail,
                thickness, NonnegativeFinite(_antiAliasingLayerWidth), NonnegativeFinite(_innerAntiAliasingLayerWidth), _mode,
                fill, edge, GraphicMesh.GetSpriteUv(overrideSprite));
            VertexCount = mesh.currentVertCount;
            TriangleCount = mesh.currentIndexCount / 3;
        }

        internal Rect GetHole(Rect panel)
        {
            var size = Vector2.Scale(Clamp01(_holeSize), panel.size);
            if (KeepAspect)
            {
                size = Vector2.one * Mathf.Min(size.x, size.y);
            }

            var center = panel.min + Vector2.Scale(Clamp01(_holeCenter), panel.size);
            var halfSize = size * 0.5f;
            center.x = Mathf.Clamp(center.x, panel.xMin + halfSize.x, panel.xMax - halfSize.x);
            center.y = Mathf.Clamp(center.y, panel.yMin + halfSize.y, panel.yMax - halfSize.y);
            return new Rect(center - halfSize, size);
        }

        private protected virtual float GetCornerRadius(Rect hole)
        {
            return 0f;
        }

        private protected void SetGeometry<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            SetVerticesDirty();
        }

        private void SetEdgeThickness(EdgeThicknessMode mode, ref float field, float value)
        {
            if (_edgeThicknessMode != mode || field != value)
            {
                _edgeThicknessMode = mode;
                field = value;
                SetVerticesDirty();
            }
        }

        private static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(NonnegativeFinite(value.x)), Mathf.Clamp01(NonnegativeFinite(value.y)));
        }

        private protected static float NonnegativeFinite(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }
    }
}
