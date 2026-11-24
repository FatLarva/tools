using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(EllipseGraphic)), CanEditMultipleObjects]
    public class CircleGraphicEditor : Editor
    {
        private const string DiagnosticsSessionKey = nameof(CircleGraphicEditor) + "Diagnostics";
        private static readonly GUIContent DashAngleLabel = new("Dash Angle (°)",
            "Visible angular length of each dash. Independent of Hull Sections.");
        private static readonly GUIContent GapAngleLabel = new("Gap Angle (°)",
            "Visible angular gap between dashes. Round-cap extensions are compensated; antialiasing feather is excluded.");
        private static readonly GUIContent DashCountLabel = new("Dash Count",
            "Number of evenly spaced dashes across Sweep Angle. Independent of Hull Sections.");
        private static readonly GUIContent SegmentGapLabel = new("Gap (UI Units)",
            "Distance between neighboring solid slice edges. Antialiasing fringe is excluded.");
        private static readonly GUIContent SegmentLayoutLabel = new("Segment Layout",
            "Distribute Sweep spreads the slices over Sweep Angle. Fixed Slots divides the full ellipse into stationary slots.");
        private static readonly GUIContent VisibleSegmentsLabel = new("Visible Segments",
            "From Start Angle, negative values reveal counterclockwise and positive values reveal clockwise. A fraction reveals part of the last slot.");
        
        private SerializedProperty _startAngleProp;
        private SerializedProperty _sweepAngleProp;
        private SerializedProperty _hullSectionsProp;
        private SerializedProperty _keepCircleProp;
        private SerializedProperty _modeProp;
        private SerializedProperty _segmentCountProp;
        private SerializedProperty _segmentLayoutProp;
        private SerializedProperty _visibleSegmentsProp;
        private SerializedProperty _innerRadiusProp;
        private SerializedProperty _segmentGapProp;
        private SerializedProperty _isDashedProp;
        private SerializedProperty _dashModeProp;
        private SerializedProperty _dashAngleProp;
        private SerializedProperty _gapAngleProp;
        private SerializedProperty _dashCountProp;
        private SerializedProperty _roundCapsProp;
        
        private bool _showDiagnostics;

        private void OnEnable()
        {
            _showDiagnostics = SessionState.GetBool(DiagnosticsSessionKey, false);
            
            _startAngleProp = serializedObject.FindProperty("_startAngle");
            _sweepAngleProp = serializedObject.FindProperty("_sweepAngle");
            _hullSectionsProp = serializedObject.FindProperty("_hullSections");
            _keepCircleProp = serializedObject.FindProperty("_keepCircle");
            _modeProp = serializedObject.FindProperty("_mode");
            _segmentCountProp = serializedObject.FindProperty("_segmentCount");
            _segmentLayoutProp = serializedObject.FindProperty("_segmentLayout");
            _visibleSegmentsProp = serializedObject.FindProperty("_visibleSegments");
            _innerRadiusProp = serializedObject.FindProperty("_innerRadius");
            _segmentGapProp = serializedObject.FindProperty("_segmentGap");
            _isDashedProp = serializedObject.FindProperty("_isDashed");
            _dashModeProp = serializedObject.FindProperty("_dashMode");
            _dashAngleProp = serializedObject.FindProperty("_dashAngle");
            _gapAngleProp = serializedObject.FindProperty("_gapAngle");
            _dashCountProp = serializedObject.FindProperty("_dashCount");
            _roundCapsProp = serializedObject.FindProperty("_roundCaps");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawShape();
            var mode = (EllipseGraphic.Mode)_modeProp.intValue;
            var mixedMode = _modeProp.hasMultipleDifferentValues;
            var hasStroke = mixedMode || mode == EllipseGraphic.Mode.Edge || mode == EllipseGraphic.Mode.EdgeAndFill;
            if (mixedMode || mode != EllipseGraphic.Mode.Edge)
            {
                PrimitiveInspector.DrawFill(serializedObject);
            }

            if (hasStroke)
            {
                DrawStroke();
            }

            PrimitiveInspector.DrawAntiAliasing(serializedObject, "_antiAliasingLayerWidth",
                hasStroke ? "_innerAntiAliasingLayerWidth" : null);
            PrimitiveInspector.DrawRendering(serializedObject);
            PrimitiveInspector.DrawInput(serializedObject);
            DrawDiagnostics();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawShape()
        {
            PrimitiveInspector.Section("Shape");
            PrimitiveInspector.DrawMode(_modeProp);
            PrimitiveInspector.Field(_startAngleProp, "Start Angle (°)");
            EditorGUILayout.PropertyField(_keepCircleProp);
            var mixedMode = _modeProp.hasMultipleDifferentValues;
            var segmentedFill = (EllipseGraphic.Mode)_modeProp.intValue == EllipseGraphic.Mode.SegmentedFill;
            if (mixedMode || segmentedFill)
            {
                EditorGUILayout.PropertyField(_segmentLayoutProp, SegmentLayoutLabel);
                PrimitiveInspector.Count(_segmentCountProp, "Segment Count", 2);
                if (_segmentLayoutProp.hasMultipleDifferentValues || (EllipseGraphic.SegmentLayout)_segmentLayoutProp.intValue == EllipseGraphic.SegmentLayout.FixedSlots)
                {
                    if (_segmentCountProp.hasMultipleDifferentValues)
                    {
                        PrimitiveInspector.Number(_visibleSegmentsProp, VisibleSegmentsLabel, minimum: -float.MaxValue);
                    }
                    else
                    {
                        var count = Mathf.Max(2, _segmentCountProp.intValue);
                        EditorGUILayout.Slider(_visibleSegmentsProp, -count, count, VisibleSegmentsLabel);
                    }
                }

                EditorGUILayout.PropertyField(_innerRadiusProp);
                PrimitiveInspector.Number(_segmentGapProp, SegmentGapLabel);
            }

            if (mixedMode || !segmentedFill || _segmentLayoutProp.hasMultipleDifferentValues ||
                (EllipseGraphic.SegmentLayout)_segmentLayoutProp.intValue == EllipseGraphic.SegmentLayout.DistributeSweep)
            {
                PrimitiveInspector.Field(_sweepAngleProp, "Sweep Angle (°)");
            }

            PrimitiveInspector.Count(_hullSectionsProp, "Hull Sections", 3);
        }

        private void DrawStroke()
        {
            PrimitiveInspector.DrawStroke(serializedObject, thicknessMode: "_edgeThicknessMode");
            if (_sweepAngleProp.hasMultipleDifferentValues || Mathf.Abs(_sweepAngleProp.floatValue) < 360f || PrimitiveInspector.CanBeEnabled(_isDashedProp))
            {
                EditorGUILayout.PropertyField(_roundCapsProp);
            }

            if (PrimitiveInspector.DrawDashToggle(serializedObject))
            {
                DrawDashes();
            }
        }

        private void DrawDashes()
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_dashModeProp);
                var dashMode = (EllipseGraphic.DashMode)_dashModeProp.intValue;
                if (_dashModeProp.hasMultipleDifferentValues || dashMode == EllipseGraphic.DashMode.Angles)
                {
                    PrimitiveInspector.Number(_dashAngleProp, DashAngleLabel, maximum: 360f);
                }

                if (_dashModeProp.hasMultipleDifferentValues || dashMode == EllipseGraphic.DashMode.Count)
                {
                    PrimitiveInspector.Count(_dashCountProp, DashCountLabel.text, 1, tooltip: DashCountLabel.tooltip);
                }

                PrimitiveInspector.Number(_gapAngleProp, GapAngleLabel, maximum: 360f);
            }
        }

        private void DrawDiagnostics()
        {
            string warning = null;
            foreach (var selected in targets)
            {
                if (selected is EllipseGraphic graphic && !string.IsNullOrEmpty(graphic.MeshDiagnostic))
                {
                    warning = graphic.MeshDiagnostic;
                    break;
                }
            }

            EditorGUI.BeginChangeCheck();
            PrimitiveInspector.DrawDiagnostics(serializedObject, ref _showDiagnostics, warning);
            if (EditorGUI.EndChangeCheck())
            {
                SessionState.SetBool(DiagnosticsSessionKey, _showDiagnostics);
            }
        }
    }
}
