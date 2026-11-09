using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(EllipseGraphic)), CanEditMultipleObjects]
    public class CircleGraphicEditor : Editor
    {
        private const string IsBuiltInShownSessionVariableKey = nameof(CircleGraphicEditor) + "IsBuiltInShown";
        
        private SerializedProperty _colorProp;
        private SerializedProperty _startAngleProp;
        private SerializedProperty _sweepAngleProp;
        private SerializedProperty _hullSectionsProp;
        private SerializedProperty _materialProp;
        private SerializedProperty _spriteProp;
        private SerializedProperty _raycastTargetProp;
        private SerializedProperty _raycastPaddingProp;
        private SerializedProperty _keepCircleProp;
        private SerializedProperty _maskableProp;
        private SerializedProperty _shouldUseGradientProp;
        private SerializedProperty _gradientColorProp;
        private SerializedProperty _gradientTypeProp;
        private SerializedProperty _antiAliasingLayerWidthProp;
        private SerializedProperty _modeProp;
        private SerializedProperty _edgeThicknessProp;
        private SerializedProperty _edgeColorProp;
        private SerializedProperty _isDashedProp;
        private SerializedProperty _roundCapsProp;
        private SerializedProperty _shouldUseGradientForEdgeProp;
        private SerializedProperty _edgeColorGradientProp;
        private SerializedProperty _edgeGradientTypeProp;
        private SerializedProperty _innerAntiAliasingLayerWidthProp;
        private SerializedProperty _edgeThicknessModeProp;
        private SerializedProperty _edgeThicknessRelativeProp;
        
        private SerializedProperty _overrideStencilProp;
        private SerializedProperty _stencilCompProp;
        private SerializedProperty _stencilRefProp;
        
        // Debug draw
        private SerializedProperty _vertCountProp;
        private SerializedProperty _trianglesCountProp;
        
        private bool _isBuiltInShown;

        private void OnEnable()
        {
            _isBuiltInShown = SessionState.GetBool(IsBuiltInShownSessionVariableKey, false);
            
            _colorProp = serializedObject.FindProperty("m_Color");
            _materialProp = serializedObject.FindProperty("m_Material");
            _spriteProp = serializedObject.FindProperty("m_Sprite");
            _raycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
            _raycastPaddingProp = serializedObject.FindProperty("m_RaycastPadding");
            _maskableProp = serializedObject.FindProperty("m_Maskable");
            _startAngleProp = serializedObject.FindProperty("_startAngle");
            _sweepAngleProp = serializedObject.FindProperty("_sweepAngle");
            _hullSectionsProp = serializedObject.FindProperty("_hullSections");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _gradientColorProp = serializedObject.FindProperty("_gradientColor");
            _gradientTypeProp = serializedObject.FindProperty("_gradientType");
            _keepCircleProp = serializedObject.FindProperty("_keepCircle");
            _antiAliasingLayerWidthProp = serializedObject.FindProperty("_antiAliasingLayerWidth");
            _modeProp = serializedObject.FindProperty("_mode");
            _isDashedProp = serializedObject.FindProperty("_isDashed");
            _roundCapsProp = serializedObject.FindProperty("_roundCaps");
            _edgeThicknessModeProp = serializedObject.FindProperty("_edgeThicknessMode");
            _edgeThicknessRelativeProp = serializedObject.FindProperty("_edgeThicknessRelative");
            _edgeThicknessProp = serializedObject.FindProperty("_edgeThickness");
            _edgeColorProp = serializedObject.FindProperty("_edgeColor");
            _shouldUseGradientForEdgeProp = serializedObject.FindProperty("_shouldUseGradientForEdge");
            _edgeColorGradientProp = serializedObject.FindProperty("_edgeColorGradient");
            _edgeGradientTypeProp = serializedObject.FindProperty("_edgeGradientType");
            _innerAntiAliasingLayerWidthProp = serializedObject.FindProperty("_innerAntiAliasingLayerWidth");
            
            _overrideStencilProp = serializedObject.FindProperty("_overrideStencil");
            _stencilCompProp = serializedObject.FindProperty("_stencilComp");
            _stencilRefProp = serializedObject.FindProperty("_stencilRef");
            
            _vertCountProp = serializedObject.FindProperty("_vertCount");
            _trianglesCountProp = serializedObject.FindProperty("_trianglesCount");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _isBuiltInShown = EditorGUILayout.Foldout(_isBuiltInShown, "BuiltIn");
            SessionState.SetBool(IsBuiltInShownSessionVariableKey, _isBuiltInShown);

            if (_isBuiltInShown)
            {
                EditorGUILayout.PropertyField(_spriteProp);
                EditorGUILayout.PropertyField(_materialProp);
                EditorGUILayout.PropertyField(_raycastTargetProp);
                EditorGUILayout.PropertyField(_raycastPaddingProp);
                EditorGUILayout.PropertyField(_maskableProp);
            }

            EditorGUILayout.PropertyField(_shouldUseGradientProp);
            if (_shouldUseGradientProp.boolValue)
            {
                EditorGUILayout.PropertyField(_gradientTypeProp);
                EditorGUILayout.PropertyField(_gradientColorProp);
            }
            else
            {
                EditorGUILayout.PropertyField(_colorProp);
            }
            
            EditorGUILayout.PropertyField(_startAngleProp);
            EditorGUILayout.PropertyField(_sweepAngleProp);
            
            EditorGUILayout.PropertyField(_hullSectionsProp);
            
            EditorGUILayout.PropertyField(_keepCircleProp);
            EditorGUILayout.PropertyField(_antiAliasingLayerWidthProp);
            
            EditorGUILayout.PropertyField(_modeProp);
            if (_modeProp.hasMultipleDifferentValues || (EllipseGraphic.Mode)_modeProp.intValue != EllipseGraphic.Mode.Fill)
            {
                EditorGUILayout.PropertyField(_isDashedProp);
                if (_sweepAngleProp.hasMultipleDifferentValues || Mathf.Abs(_sweepAngleProp.floatValue) < 360f ||
                    _isDashedProp.hasMultipleDifferentValues || _isDashedProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_roundCapsProp);
                }

                EditorGUILayout.PropertyField(_edgeThicknessModeProp);
                var thicknessMode = (EllipseGraphic.EdgeThicknessMode)_edgeThicknessModeProp.intValue;
                if (_edgeThicknessModeProp.hasMultipleDifferentValues || thicknessMode == EllipseGraphic.EdgeThicknessMode.Absolute)
                {
                    EditorGUILayout.PropertyField(_edgeThicknessProp);
                }

                if (_edgeThicknessModeProp.hasMultipleDifferentValues || thicknessMode == EllipseGraphic.EdgeThicknessMode.Relative)
                {
                    EditorGUILayout.PropertyField(_edgeThicknessRelativeProp);
                }

                EditorGUILayout.PropertyField(_shouldUseGradientForEdgeProp);
                if (_shouldUseGradientForEdgeProp.boolValue)
                {
                    EditorGUILayout.PropertyField(_edgeGradientTypeProp);
                    EditorGUILayout.PropertyField(_edgeColorGradientProp);
                }
                else
                {
                    EditorGUILayout.PropertyField(_edgeColorProp);
                }
                
                EditorGUILayout.PropertyField(_innerAntiAliasingLayerWidthProp);
            }

            EditorGUILayout.PropertyField(_overrideStencilProp);
            if (_overrideStencilProp.boolValue)
            {
                EditorGUILayout.PropertyField(_stencilCompProp);
                EditorGUILayout.PropertyField(_stencilRefProp);
            }
            
            /*EditorGUILayout.PropertyField(_isDashedProp);
            if (_isDashedProp.boolValue)
            {
                EditorGUILayout.PropertyField(_dashLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashGapLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashShiftProp);
            }*/

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_vertCountProp);
                EditorGUILayout.PropertyField(_trianglesCountProp);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
