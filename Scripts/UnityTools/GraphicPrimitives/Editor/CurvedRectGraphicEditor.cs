using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(CurvedRectGraphic)), CanEditMultipleObjects]
    public class CurvedRectGraphicEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var serialized = serializedObject;
            serialized.Update();
            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serialized);
            PrimitiveInspector.Field(serialized, "_topCurveAmount", "Half Profile");
            PrimitiveInspector.Count(serialized.FindProperty("_curveQuality"), "Samples per Curve", 3, 100);
            ClosedShapeInspector.DrawAppearance(mode, "_antiAliasingLayerWidth");
            serialized.ApplyModifiedProperties();
        }
    }
}
