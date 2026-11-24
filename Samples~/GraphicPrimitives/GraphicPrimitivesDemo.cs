using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityTools.GraphicPrimitives;

namespace FatLarva.Tools.Samples
{
    public sealed class GraphicPrimitivesDemo : MonoBehaviour
    {
        private const string InputSystemModuleName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        [SerializeField] private EllipseGraphic _ellipse;
        [SerializeField] private RoundedRectGraphic _roundedRect;
        [SerializeField] private ArrowGraphic _arrow;
        [SerializeField] private SunburstGraphic _sunburst;
        [SerializeField] private InversedEllipseGraphic _inversedEllipse;
        [SerializeField] private InversedRectGraphic _inversedRect;

        [SerializeField] private Slider _sweepSlider;
        [SerializeField] private Text _sweepLabel;
        [SerializeField] private Toggle _fixedSlotsToggle;
        [SerializeField] private Slider _radiusSlider;
        [SerializeField] private Slider _arrowStrokeSlider;
        [SerializeField] private Toggle _sunburstSmoothToggle;
        [SerializeField] private Slider _holeSizeSlider;
        [SerializeField] private Button _hitTarget;
        [SerializeField] private Text _hitStatus;
        [SerializeField] private Button _resetButton;
        [SerializeField] private EventSystem _eventSystem;

        private float _initialSweep;
        private float _initialVisibleSegments;
        private EllipseGraphic.SegmentLayout _initialSegmentLayout;
        private float _initialRadius;
        private float _initialStroke;
        private bool _initialSmooth;
        private Vector2 _initialEllipseHoleSize;
        private Vector2 _initialRectHoleSize;
        private string _initialHitStatus;
        private int _hitCount;
        private bool _initialized;
        private bool _updatingEllipseControls;
        private bool _hasInverseControls;

        private void Awake()
        {
            if (_ellipse == null || _roundedRect == null || _arrow == null || _sunburst == null ||
                _sweepSlider == null || _radiusSlider == null || _arrowStrokeSlider == null ||
                _sunburstSmoothToggle == null || _hitTarget == null || _hitStatus == null ||
                _resetButton == null || _eventSystem == null)
            {
                Debug.LogError("Assign the Graphic Primitives demo shapes, controls, and EventSystem.", this);
                enabled = false;
                return;
            }

            _initialSweep = _ellipse.SweepAngle;
            _initialVisibleSegments = _ellipse.VisibleSegments;
            _initialSegmentLayout = _ellipse.SegmentedFillLayout;
            _initialRadius = _roundedRect.CornerRadius;
            _initialStroke = _arrow.StrokeThickness;
            _initialSmooth = _sunburst.Profile == SunburstGraphic.ProfileType.Smooth;
            _hasInverseControls = _inversedEllipse != null && _inversedRect != null && _holeSizeSlider != null;
            if (_hasInverseControls)
            {
                _initialEllipseHoleSize = _inversedEllipse.HoleSize;
                _initialRectHoleSize = _inversedRect.HoleSize;
                _holeSizeSlider.SetValueWithoutNotify(1f);
            }

            _initialHitStatus = _hitStatus.text;
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            ConfigureInputModule();
            UpdateEllipseControls();
            _radiusSlider.SetValueWithoutNotify(_roundedRect.CornerRadius);
            _arrowStrokeSlider.SetValueWithoutNotify(_arrow.StrokeThickness);
            _sunburstSmoothToggle.SetIsOnWithoutNotify(_sunburst.Profile == SunburstGraphic.ProfileType.Smooth);

            _sweepSlider.onValueChanged.AddListener(SetEllipseProgress);
            if (_fixedSlotsToggle != null)
            {
                _fixedSlotsToggle.onValueChanged.AddListener(SetFixedSlots);
            }
            _radiusSlider.onValueChanged.AddListener(SetRoundedRadius);
            _arrowStrokeSlider.onValueChanged.AddListener(SetArrowStroke);
            _sunburstSmoothToggle.onValueChanged.AddListener(SetSunburstSmooth);
            if (_hasInverseControls)
            {
                _holeSizeSlider.onValueChanged.AddListener(SetHoleSizeScale);
            }

            _hitTarget.onClick.AddListener(HitInvisibleTarget);
            _resetButton.onClick.AddListener(ResetControls);
        }

        private void OnDisable()
        {
            if (!_initialized)
            {
                return;
            }

            _sweepSlider.onValueChanged.RemoveListener(SetEllipseProgress);
            if (_fixedSlotsToggle != null)
            {
                _fixedSlotsToggle.onValueChanged.RemoveListener(SetFixedSlots);
            }
            _radiusSlider.onValueChanged.RemoveListener(SetRoundedRadius);
            _arrowStrokeSlider.onValueChanged.RemoveListener(SetArrowStroke);
            _sunburstSmoothToggle.onValueChanged.RemoveListener(SetSunburstSmooth);
            if (_hasInverseControls)
            {
                _holeSizeSlider.onValueChanged.RemoveListener(SetHoleSizeScale);
            }

            _hitTarget.onClick.RemoveListener(HitInvisibleTarget);
            _resetButton.onClick.RemoveListener(ResetControls);
        }

        public void SetEllipseSweep(float angle)
        {
            _ellipse.SweepAngle = angle;
            UpdateEllipseControls();
        }

        private void SetEllipseProgress(float value)
        {
            if (_updatingEllipseControls)
            {
                return;
            }

            if (_ellipse.SegmentedFillLayout == EllipseGraphic.SegmentLayout.FixedSlots)
            {
                _ellipse.VisibleSegments = value;
            }
            else
            {
                _ellipse.SweepAngle = value;
            }
        }

        private void SetFixedSlots(bool enabled)
        {
            _ellipse.SegmentedFillLayout = enabled
                ? EllipseGraphic.SegmentLayout.FixedSlots
                : EllipseGraphic.SegmentLayout.DistributeSweep;
            UpdateEllipseControls();
        }

        private void UpdateEllipseControls()
        {
            _updatingEllipseControls = true;
            try
            {
                bool fixedSlots = _ellipse.SegmentedFillLayout == EllipseGraphic.SegmentLayout.FixedSlots;
                float limit = fixedSlots ? _ellipse.SegmentCount : 360f;
                _sweepSlider.minValue = -limit;
                _sweepSlider.maxValue = limit;
                _sweepSlider.SetValueWithoutNotify(fixedSlots ? _ellipse.VisibleSegments : _ellipse.SweepAngle);
                if (_sweepLabel != null)
                {
                    _sweepLabel.text = fixedSlots ? "Visible Segments" : "Sweep";
                }

                if (_fixedSlotsToggle != null)
                {
                    _fixedSlotsToggle.SetIsOnWithoutNotify(fixedSlots);
                }
            }
            finally
            {
                _updatingEllipseControls = false;
            }
        }

        public void SetRoundedRadius(float radius)
        {
            _roundedRect.CornerRadius = radius;
            _radiusSlider.SetValueWithoutNotify(_roundedRect.CornerRadius);
        }

        public void SetArrowStroke(float thickness)
        {
            _arrow.StrokeThickness = thickness;
            _arrowStrokeSlider.SetValueWithoutNotify(_arrow.StrokeThickness);
        }

        public void SetSunburstSmooth(bool smooth)
        {
            _sunburst.Profile = smooth ? SunburstGraphic.ProfileType.Smooth : SunburstGraphic.ProfileType.Sharp;
            _sunburstSmoothToggle.SetIsOnWithoutNotify(smooth);
        }

        public void SetHoleSizeScale(float scale)
        {
            if (!_hasInverseControls || !float.IsFinite(scale))
            {
                return;
            }

            scale = Mathf.Clamp(scale, _holeSizeSlider.minValue, _holeSizeSlider.maxValue);
            _inversedEllipse.HoleSize = _initialEllipseHoleSize * scale;
            _inversedRect.HoleSize = _initialRectHoleSize * scale;
            _holeSizeSlider.SetValueWithoutNotify(scale);
        }

        public void HitInvisibleTarget()
        {
            _hitCount++;
            _hitStatus.text = $"Clicks: {_hitCount}";
        }

        public void ResetControls()
        {
            _ellipse.SegmentedFillLayout = _initialSegmentLayout;
            _ellipse.VisibleSegments = _initialVisibleSegments;
            SetEllipseSweep(_initialSweep);
            SetRoundedRadius(_initialRadius);
            SetArrowStroke(_initialStroke);
            SetSunburstSmooth(_initialSmooth);
            SetHoleSizeScale(1f);
            _hitCount = 0;
            _hitStatus.text = _initialHitStatus;
        }

        private void ConfigureInputModule()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var legacyModule = _eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                legacyModule.enabled = false;
            }

            var moduleType = System.Type.GetType(InputSystemModuleName);
            if (moduleType == null)
            {
                Debug.LogError("The Input System UI module is unavailable in this project.", this);
                return;
            }

            // A newly enabled Input System UI module supplies its own default actions.
            var module = (BaseInputModule)_eventSystem.GetComponent(moduleType);
            if (module == null)
            {
                module = (BaseInputModule)_eventSystem.gameObject.AddComponent(moduleType);
            }

            module.enabled = true;
#endif
        }
    }
}
