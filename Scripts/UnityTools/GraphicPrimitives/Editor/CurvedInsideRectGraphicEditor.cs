using UnityEditor;
using UnityEditor.UI;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(CurvedInsideRectGraphic)), CanEditMultipleObjects]
    public class CurvedInsideRectGraphicEditor : ImageEditor
    {
        private SerializedProperty _topCurveAmountProp;
        private SerializedProperty _curveQualityProp;
        private SerializedProperty _gradientColorProp;
        private SerializedProperty _shouldUseGradientProp;
        private SerializedProperty _gradientTypeProp;
        private SerializedProperty _antiAliasingWidthProp;
        private SerializedProperty _overrideStencilProp;
        private SerializedProperty _stencilCompProp;
        private SerializedProperty _stencilRefProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            _topCurveAmountProp = serializedObject.FindProperty("_topCurveAmount");
            _curveQualityProp = serializedObject.FindProperty("_curveQuality");
            _gradientColorProp = serializedObject.FindProperty("_gradientColor");
            _shouldUseGradientProp = serializedObject.FindProperty("_shouldUseGradient");
            _gradientTypeProp = serializedObject.FindProperty("_gradientType");
            _antiAliasingWidthProp = serializedObject.FindProperty("_antiAliasingWidth");
            _overrideStencilProp = serializedObject.FindProperty("_overrideStencil");
            _stencilCompProp = serializedObject.FindProperty("_stencilComp");
            _stencilRefProp = serializedObject.FindProperty("_stencilRef");
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
            EditorGUILayout.PropertyField(_antiAliasingWidthProp);
            
            EditorGUILayout.PropertyField(_overrideStencilProp);
            if (_overrideStencilProp.boolValue)
            {
                EditorGUILayout.PropertyField(_stencilCompProp);
                EditorGUILayout.PropertyField(_stencilRefProp);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
