using UnityEditor;
using UnityEditor.UI;
using UnityTools.GraphicPrimitives;
using EdgeType = UnityTools.GraphicPrimitives.RoundedRectGraphic.EdgeType;
using EdgeThicknessType = UnityTools.GraphicPrimitives.RoundedRectGraphic.EdgeThicknessType;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(SunburstGraphic)), CanEditMultipleObjects]
    public class SunburstGraphicEditor : ImageEditor
    {
        private const string IsBuiltInShownSessionVariableKey = nameof(SunburstGraphicEditor) + "IsBuiltInShown";
        
        private SerializedProperty _startAngleProp;
        private SerializedProperty _peaksCountProp;
        private SerializedProperty _keepCircleProp;
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
        
        // Debug draw
        private SerializedProperty _vertCountProp;
        private SerializedProperty _trianglesCountProp;
        
        private bool _isBuiltInShown;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            _isBuiltInShown = SessionState.GetBool(IsBuiltInShownSessionVariableKey, false);
            
            _startAngleProp = serializedObject.FindProperty("_startAngle");
            _peaksCountProp = serializedObject.FindProperty("_peaksCount");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _colorGradientProp = serializedObject.FindProperty("_gradientColor");
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
                SpriteGUI();
                EditorGUILayout.PropertyField(m_Material);
                RaycastControlsGUI();
                MaskableControlsGUI();
            }

            EditorGUILayout.PropertyField(_shouldUseGradientProp);
            if (_shouldUseGradientProp.boolValue)
            {
                EditorGUILayout.PropertyField(_gradientTypeProp);
                EditorGUILayout.PropertyField(_colorGradientProp);
            }
            else
            {
                EditorGUILayout.PropertyField(m_Color);
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
