using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    using EdgeType = RoundedRectGraphic.EdgeType;
    using EdgeThicknessType = RoundedRectGraphic.EdgeThicknessType;

    [CustomEditor(typeof(RoundedRectGraphic)), CanEditMultipleObjects]
    public class RoundedRectGraphicEditor : Editor
    {
        private SerializedProperty _colorProp;
        private SerializedProperty _cornerRadiusProp;
        private SerializedProperty _cornerQualityProp;
        private SerializedProperty _materialProp;
        private SerializedProperty _spriteProp;
        private SerializedProperty _raycastTargetProp;
        private SerializedProperty _raycastPaddingProp;
        private SerializedProperty _keepSquareProp;
        private SerializedProperty _maskableProp;
        private SerializedProperty _shouldUseGradientProp;
        private SerializedProperty _colorGradientProp;
        private SerializedProperty _gradientTypeProp;
        private SerializedProperty _separateRoundingForCornersProp;
        private SerializedProperty _cornersRoundingsProp;
        private SerializedProperty _antiAliasingBorderThicknessProp;
        private SerializedProperty _edgeTypeProp;
        private SerializedProperty _edgeThicknessProp;
        private SerializedProperty _edgeColorProp;
        private SerializedProperty _shouldUseGradientForEdgeProp;
        private SerializedProperty _edgeColorGradientProp;
        private SerializedProperty _edgeGradientTypeProp;
        private SerializedProperty _innerAaThicknessProp;
        private SerializedProperty _edgeThicknessTypeProp;
        private SerializedProperty _edgeThicknessRelativeProp;

        private SerializedProperty _isDashedProp;
        private SerializedProperty _dashLengthPixelsProp;
        private SerializedProperty _dashGapLengthPixelsProp;
        private SerializedProperty _dashShiftProp;
        
        // Debug draw
        private SerializedProperty _vertCountProp;
        private SerializedProperty _trianglesCountProp;
        
        private bool _isBuiltInShown;

        private void OnEnable()
        {
            _colorProp = serializedObject.FindProperty("m_Color");
            _cornerRadiusProp = serializedObject.FindProperty("_cornerRadius");
            _cornerQualityProp = serializedObject.FindProperty("_cornerQuality");
            _materialProp = serializedObject.FindProperty("m_Material");
            _spriteProp = serializedObject.FindProperty("m_Sprite");
            _raycastTargetProp = serializedObject.FindProperty("m_RaycastTarget");
            _raycastPaddingProp = serializedObject.FindProperty("m_RaycastPadding");
            _maskableProp = serializedObject.FindProperty("m_Maskable");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _colorGradientProp = serializedObject.FindProperty("_colorGradient");
            _gradientTypeProp = serializedObject.FindProperty("_gradientType");
            _keepSquareProp = serializedObject.FindProperty("_keepSquare");
            _separateRoundingForCornersProp = serializedObject.FindProperty("_separateRoundingForCorners");
            _cornersRoundingsProp = serializedObject.FindProperty("_cornersRoundings");
            _antiAliasingBorderThicknessProp = serializedObject.FindProperty("_antiAliasingBorderThickness");
            _edgeTypeProp = serializedObject.FindProperty("_edgeType");
            _edgeThicknessProp = serializedObject.FindProperty("_edgeThickness");
            _edgeColorProp = serializedObject.FindProperty("_edgeColor");
            _shouldUseGradientForEdgeProp = serializedObject.FindProperty("_shouldUseGradientForEdge");
            _edgeColorGradientProp = serializedObject.FindProperty("_edgeColorGradient");
            _edgeGradientTypeProp = serializedObject.FindProperty("_edgeGradientType");
            _innerAaThicknessProp = serializedObject.FindProperty("_innerAaThickness");
            _edgeThicknessTypeProp = serializedObject.FindProperty("_edgeThicknessType");
            _edgeThicknessRelativeProp = serializedObject.FindProperty("_edgeThicknessRelative");
            
            _isDashedProp = serializedObject.FindProperty("_isDashed");
            _dashLengthPixelsProp = serializedObject.FindProperty("_dashLengthPixels");
            _dashGapLengthPixelsProp = serializedObject.FindProperty("_dashGapLengthPixels");
            _dashShiftProp = serializedObject.FindProperty("_dashShift");
            
            _vertCountProp = serializedObject.FindProperty("_vertCount");
            _trianglesCountProp = serializedObject.FindProperty("_trianglesCount");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _isBuiltInShown = EditorGUILayout.Foldout(_isBuiltInShown, "BuiltIn");

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
            
            EditorGUILayout.PropertyField(_cornerQualityProp);
            
            EditorGUILayout.PropertyField(_separateRoundingForCornersProp);
            if (_separateRoundingForCornersProp.boolValue)
            {
                EditorGUILayout.PropertyField(_cornersRoundingsProp);
            }
            else
            {
                EditorGUILayout.PropertyField(_cornerRadiusProp);
            }
            
            EditorGUILayout.PropertyField(_keepSquareProp);
            EditorGUILayout.PropertyField(_antiAliasingBorderThicknessProp);
            
            EditorGUILayout.PropertyField(_edgeTypeProp);
            if ((EdgeType)_edgeTypeProp.enumValueIndex != EdgeType.None)
            {
                EditorGUILayout.PropertyField(_edgeThicknessTypeProp);
                if ((EdgeThicknessType)_edgeThicknessTypeProp.enumValueIndex == EdgeThicknessType.Absolute)
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
                
                EditorGUILayout.PropertyField(_innerAaThicknessProp);
            }

            EditorGUILayout.PropertyField(_isDashedProp);
            if (_isDashedProp.boolValue)
            {
                EditorGUILayout.PropertyField(_dashLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashGapLengthPixelsProp);
                EditorGUILayout.PropertyField(_dashShiftProp);
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
