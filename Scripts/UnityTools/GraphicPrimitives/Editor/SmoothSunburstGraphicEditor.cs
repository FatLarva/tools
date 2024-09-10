using UnityEditor;
using UnityTools.GraphicPrimitives;
using EdgeType = UnityTools.GraphicPrimitives.RoundedRectGraphic.EdgeType;
using EdgeThicknessType = UnityTools.GraphicPrimitives.RoundedRectGraphic.EdgeThicknessType;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(SmoothSunburstGraphic)), CanEditMultipleObjects]
    public class SmoothSunburstGraphicEditor : Editor
    {
        private const string IsBuiltInShownSessionVariableKey = nameof(SmoothSunburstGraphicEditor) + "IsBuiltInShown";
        
        private SerializedProperty _colorProp;
        private SerializedProperty _startAngleProp;
        private SerializedProperty _peaksCountProp;
        private SerializedProperty _materialProp;
        private SerializedProperty _spriteProp;
        private SerializedProperty _raycastTargetProp;
        private SerializedProperty _raycastPaddingProp;
        private SerializedProperty _keepCircleProp;
        private SerializedProperty _maskableProp;
        private SerializedProperty _shouldUseGradientProp;
        private SerializedProperty _colorGradientProp;
        private SerializedProperty _gradientTypeProp;
        private SerializedProperty _antiAliasingLayerWidthProp;
        private SerializedProperty _modeProp;
        private SerializedProperty _edgeThicknessProp;
        private SerializedProperty _edgeColorProp;
        private SerializedProperty _isDashedProp;
        private SerializedProperty _shouldUseGradientForEdgeProp;
        private SerializedProperty _edgeColorGradientProp;
        private SerializedProperty _edgeGradientTypeProp;
        private SerializedProperty _innerAntiAliasingLayerWidthProp;
        private SerializedProperty _edgeThicknessModeProp;
        private SerializedProperty _edgeThicknessRelativeProp;
        private SerializedProperty _outerDisplacementFactorProp;
        private SerializedProperty _innerDisplacementFactorProp;
        private SerializedProperty _outerTangentPointShiftProp;
        private SerializedProperty _innerTangentPointShiftProp;
        private SerializedProperty _outerTangentPointShiftPerpProp;
        private SerializedProperty _innerTangentPointShiftPerpProp;
        private SerializedProperty _curvesSmoothnessProp;
        
        private SerializedProperty _overrideStencilProp;
        private SerializedProperty _stencilCompProp;
        private SerializedProperty _stencilOpProp;
        private SerializedProperty _stencilRefProp;
        private SerializedProperty _hideMaskGraphicsProp;
        
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
            _peaksCountProp = serializedObject.FindProperty("_peaksCount");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _colorGradientProp = serializedObject.FindProperty("_colorGradient");
            _gradientTypeProp = serializedObject.FindProperty("_gradientType");
            _keepCircleProp = serializedObject.FindProperty("_keepCircle");
            _antiAliasingLayerWidthProp = serializedObject.FindProperty("_antiAliasingLayerWidth");
            _modeProp = serializedObject.FindProperty("_mode");
            _isDashedProp = serializedObject.FindProperty("_isDashed");
            _edgeThicknessModeProp = serializedObject.FindProperty("_edgeThicknessMode");
            _edgeThicknessRelativeProp = serializedObject.FindProperty("_edgeThicknessRelative");
            _edgeThicknessProp = serializedObject.FindProperty("_edgeThickness");
            _edgeColorProp = serializedObject.FindProperty("_edgeColor");
            _shouldUseGradientForEdgeProp = serializedObject.FindProperty("_shouldUseGradientForEdge");
            _edgeColorGradientProp = serializedObject.FindProperty("_edgeColorGradient");
            _edgeGradientTypeProp = serializedObject.FindProperty("_edgeGradientType");
            _innerAntiAliasingLayerWidthProp = serializedObject.FindProperty("_innerAntiAliasingLayerWidth");
            
            _outerDisplacementFactorProp = serializedObject.FindProperty("_outerDisplacementFactor");
            _innerDisplacementFactorProp = serializedObject.FindProperty("_innerDisplacementFactor");
            
            _outerTangentPointShiftProp = serializedObject.FindProperty("_outerTangentPointShift");
            _innerTangentPointShiftProp = serializedObject.FindProperty("_innerTangentPointShift");
            
            _outerTangentPointShiftPerpProp = serializedObject.FindProperty("_outerTangentPointShiftPerp");
            _innerTangentPointShiftPerpProp = serializedObject.FindProperty("_innerTangentPointShiftPerp");
            
            _curvesSmoothnessProp = serializedObject.FindProperty("_curvesSmoothness");
            
            _overrideStencilProp = serializedObject.FindProperty("_overrideStencil");
            _stencilCompProp = serializedObject.FindProperty("_stencilComp");
            _stencilOpProp = serializedObject.FindProperty("_stencilOp");
            _stencilRefProp = serializedObject.FindProperty("_stencilRef");
            _hideMaskGraphicsProp = serializedObject.FindProperty("_hideMaskGraphics");
            
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
                EditorGUILayout.PropertyField(_colorGradientProp);
            }
            else
            {
                EditorGUILayout.PropertyField(_colorProp);
            }
            
            EditorGUILayout.PropertyField(_startAngleProp);
            
            EditorGUILayout.PropertyField(_peaksCountProp);
            
            EditorGUILayout.PropertyField(_keepCircleProp);
            EditorGUILayout.PropertyField(_isDashedProp);
            EditorGUILayout.PropertyField(_antiAliasingLayerWidthProp);
            
            EditorGUILayout.PropertyField(_modeProp);
            if ((EdgeType)_modeProp.enumValueIndex != EdgeType.None)
            {
                EditorGUILayout.PropertyField(_edgeThicknessModeProp);
                if ((EdgeThicknessType)_edgeThicknessModeProp.enumValueIndex == EdgeThicknessType.Absolute)
                {
                    EditorGUILayout.PropertyField(_edgeThicknessProp);
                }
                else
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

            EditorGUILayout.PropertyField(_outerDisplacementFactorProp);
            EditorGUILayout.PropertyField(_innerDisplacementFactorProp);
            
            EditorGUILayout.PropertyField(_outerTangentPointShiftProp);
            EditorGUILayout.PropertyField(_innerTangentPointShiftProp);
            EditorGUILayout.PropertyField(_outerTangentPointShiftPerpProp);
            EditorGUILayout.PropertyField(_innerTangentPointShiftPerpProp);
            
            EditorGUILayout.PropertyField(_curvesSmoothnessProp);
            
            /*EditorGUILayout.PropertyField(_isDashedProp);
            if (_isDashedProp.boolValue)
            {
                EditorGUILayout.PropertyField(_dashLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashGapLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashShiftProp);
            }*/

            EditorGUILayout.PropertyField(_overrideStencilProp);
            if (_overrideStencilProp.boolValue)
            {
                EditorGUILayout.PropertyField(_stencilCompProp);
                EditorGUILayout.PropertyField(_stencilOpProp);
                EditorGUILayout.PropertyField(_stencilRefProp);
                EditorGUILayout.PropertyField(_hideMaskGraphicsProp);
            }
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(_vertCountProp);
                EditorGUILayout.PropertyField(_trianglesCountProp);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
