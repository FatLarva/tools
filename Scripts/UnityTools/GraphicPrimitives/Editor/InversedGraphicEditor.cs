using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(InversedGraphic), true), CanEditMultipleObjects]
    public sealed class InversedGraphicEditor : Editor
    {
        private static readonly GUIContent HoleCenterLabel = new("Hole Center",
            "Center in normalized panel coordinates. (0, 0) is the bottom-left and (1, 1) is the top-right. The visible center shifts inward when the hole reaches an edge.");
        private static readonly GUIContent HoleSizeLabel = new("Hole Size",
            "Width and height as fractions of the panel. A zero dimension removes the hole.");
        private static readonly GUIContent CornerRadiusLabel = new("Corner Radius",
            "Corner radius as a fraction of the shorter side of the rectangular hole.");

        private bool _showDiagnostics;

        public override void OnInspectorGUI()
        {
            var serialized = serializedObject;
            serialized.Update();

            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serialized);
            DrawShape(serialized);

            var drawFill = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Edge;
            var drawStroke = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Fill;
            if (drawFill)
            {
                PrimitiveInspector.DrawFill(serialized);
            }

            if (drawStroke)
            {
                PrimitiveInspector.DrawStroke(serialized, thicknessMode: "_edgeThicknessMode");
            }

            PrimitiveInspector.DrawAntiAliasing(serialized, "_antiAliasingLayerWidth",
                drawStroke ? "_innerAntiAliasingLayerWidth" : null);
            PrimitiveInspector.DrawRendering(serialized);
            PrimitiveInspector.DrawInput(serialized);
            serialized.ApplyModifiedProperties();

            var graphic = targets.Length == 1 ? (InversedGraphic)target : null;
            PrimitiveInspector.DrawDiagnostics(ref _showDiagnostics, graphic?.VertexCount, graphic?.TriangleCount, GetMeshWarning());
        }

        private static void DrawShape(SerializedObject serialized)
        {
            DrawNormalizedVector(serialized.FindProperty("_holeCenter"), HoleCenterLabel, "X", "Y");
            DrawNormalizedVector(serialized.FindProperty("_holeSize"), HoleSizeLabel, "Width", "Height");

            var hullSections = serialized.FindProperty("_hullSections");
            if (hullSections != null)
            {
                PrimitiveInspector.Field(serialized, "_keepCircle");
                PrimitiveInspector.Count(hullSections, "Hull Sections", 4, InverseContour.MaximumEllipseDetail,
                    "Number of samples along the elliptical hole.");
            }

            var cornerSegments = serialized.FindProperty("_cornerQuality");
            if (cornerSegments != null)
            {
                PrimitiveInspector.Field(serialized, "_keepSquare");
                EditorGUILayout.Slider(serialized.FindProperty("_cornerRadius"), 0f, 0.5f, CornerRadiusLabel);
                PrimitiveInspector.Count(cornerSegments, "Corner Segments", 1, 64, "Number of samples per rounded corner of the hole.");
            }
        }

        private static void DrawNormalizedVector(SerializedProperty property, GUIContent label, string horizontal, string vertical)
        {
            EditorGUILayout.LabelField(label);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.Slider(property.FindPropertyRelative("x"), 0f, 1f, horizontal);
                EditorGUILayout.Slider(property.FindPropertyRelative("y"), 0f, 1f, vertical);
            }
        }

        private string GetMeshWarning()
        {
            foreach (var selected in targets)
            {
                if (selected is InversedGraphic graphic && graphic.MeshBudgetExceeded)
                {
                    return "The cutout exceeds the mesh vertex limit. Reduce the number of contour samples.";
                }
            }

            return null;
        }
    }
}
