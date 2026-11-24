using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(TriangleGraphic)), CanEditMultipleObjects]
    public class TriangleGraphicEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PrimitiveInspector.Section("Shape");
            PrimitiveInspector.Field(serializedObject, "_roundingRadiusX", "Corner Radius X");
            PrimitiveInspector.Field(serializedObject, "_roundingRadiusY", "Corner Radius Y");
            PrimitiveInspector.Count(serializedObject.FindProperty("_roundingQuality"), "Corner Segments", 3, 50);
            PrimitiveInspector.DrawFill(serializedObject);
            PrimitiveInspector.DrawStroke(serializedObject, gradientEnabled: null);
            PrimitiveInspector.DrawAntiAliasing(serializedObject, "_antiAliasingLayerWidth", "_innerAntiAliasingLayerWidth");
            PrimitiveInspector.DrawRendering(serializedObject);
            PrimitiveInspector.DrawInput(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
