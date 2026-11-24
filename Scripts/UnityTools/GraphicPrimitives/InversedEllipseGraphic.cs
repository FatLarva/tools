using UnityEngine;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Inversed Ellipse Graphic", 12)]
    public sealed class InversedEllipseGraphic : InversedGraphic
    {
        [SerializeField] private bool _keepCircle;
        [SerializeField, Range(4, InverseContour.MaximumEllipseDetail)] private int _hullSections = 64;

        private protected override bool IsEllipse => true;
        private protected override bool KeepAspect => _keepCircle;
        private protected override int Detail => _hullSections;

        public bool KeepCircle
        {
            get { return _keepCircle; }
            set { SetGeometry(ref _keepCircle, value); }
        }

        public int HullSections
        {
            get { return _hullSections; }
            set { SetGeometry(ref _hullSections, Mathf.Clamp(value, 4, InverseContour.MaximumEllipseDetail)); }
        }
    }
}
