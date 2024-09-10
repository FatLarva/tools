using UnityEditor;
using UnityEditor.UI;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(CurvedRectGraphic)), CanEditMultipleObjects]
    public class CurvedRectGraphicEditor : ImageEditor
    {
        private SerializedProperty _topCurveAmountProp;
        private SerializedProperty _gradientColorProp;
        private SerializedProperty _curveQualityProp;
        private SerializedProperty _shouldUseGradientProp;
        private SerializedProperty _gradientTypeProp;
        
        private SerializedProperty _edgeThicknessProp;
        private SerializedProperty _antiAliasingLayerWidthProp;
        private SerializedProperty _innerAntiAliasingLayerWidthProp;
        private SerializedProperty _edgeColorProp;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            _topCurveAmountProp = serializedObject.FindProperty("_topCurveAmount");
            _curveQualityProp = serializedObject.FindProperty("_curveQuality");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _gradientTypeProp = serializedObject.FindProperty("_gradientType");
            _gradientColorProp = serializedObject.FindProperty("_gradientColor");
            
            _edgeThicknessProp = serializedObject.FindProperty("_edgeThickness");
            _antiAliasingLayerWidthProp = serializedObject.FindProperty("_antiAliasingLayerWidth");
            _innerAntiAliasingLayerWidthProp = serializedObject.FindProperty("_innerAntiAliasingLayerWidth");
            _edgeColorProp = serializedObject.FindProperty("_edgeColor");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            
            EditorGUILayout.PropertyField(_topCurveAmountProp);
            EditorGUILayout.PropertyField(_curveQualityProp);
            EditorGUILayout.PropertyField(_shouldUseGradientProp);
            if (_shouldUseGradientProp.boolValue)
            {
                EditorGUILayout.PropertyField(_gradientColorProp);
                EditorGUILayout.PropertyField(_gradientTypeProp);
            }
            
            EditorGUILayout.PropertyField(_edgeThicknessProp);
            EditorGUILayout.PropertyField(_antiAliasingLayerWidthProp);
            EditorGUILayout.PropertyField(_innerAntiAliasingLayerWidthProp);
            EditorGUILayout.PropertyField(_edgeColorProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
