using UnityEngine;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Inversed Rect Graphic", 12)]
    public sealed class InversedRectGraphic : InversedGraphic
    {
        [SerializeField] private bool _keepSquare;
        [SerializeField, Range(0f, 0.5f)] private float _cornerRadius = 0.15f;
        [SerializeField, Range(1, 64)] private int _cornerQuality = 12;

        private protected override bool IsEllipse => false;
        private protected override bool KeepAspect => _keepSquare;
        private protected override int Detail => Mathf.Clamp(_cornerQuality, 1, 64);

        public bool KeepSquare
        {
            get { return _keepSquare; }
            set { SetGeometry(ref _keepSquare, value); }
        }

        /// <summary>Corner radius as a fraction of the shorter side of the hole.</summary>
        public float CornerRadius
        {
            get { return _cornerRadius; }
            set { SetGeometry(ref _cornerRadius, Mathf.Min(NonnegativeFinite(value), 0.5f)); }
        }

        public int CornerSegments
        {
            get { return _cornerQuality; }
            set { SetGeometry(ref _cornerQuality, Mathf.Clamp(value, 1, 64)); }
        }

        private protected override float GetCornerRadius(Rect hole)
        {
            return Mathf.Min(NonnegativeFinite(_cornerRadius), 0.5f) * Mathf.Min(hole.width, hole.height);
        }
    }
}
