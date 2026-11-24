using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    using EdgeType = RoundedRectGraphic.EdgeType;

    [CustomEditor(typeof(RoundedRectGraphic)), CanEditMultipleObjects]
    public class RoundedRectGraphicEditor : Editor
    {
        private bool _showDiagnostics;

        public override void OnInspectorGUI()
        {
            var serialized = serializedObject;
            serialized.Update();
            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serialized, "_edgeType");
            DrawShape(serialized);
            var drawFill = mode.hasMultipleDifferentValues || (EdgeType)mode.intValue != EdgeType.Edge;
            var drawStroke = mode.hasMultipleDifferentValues || (EdgeType)mode.intValue != EdgeType.Fill;
            if (drawFill)
            {
                PrimitiveInspector.DrawFill(serialized);
            }

            if (drawStroke)
            {
                // RoundedRect stores the ratio of the whole shorter side.
                PrimitiveInspector.DrawStroke(serialized, thicknessMode: "_edgeThicknessType", relativeThicknessScale: 2f);
                PrimitiveInspector.DrawPerimeterDashes(serialized, "_dashLengthPixels", "_dashGapLengthPixels", "_dashShift");
            }

            PrimitiveInspector.DrawAntiAliasing(serialized, "_antiAliasingBorderThickness", drawStroke ? "_innerAaThickness" : null);
            PrimitiveInspector.DrawRendering(serialized);
            PrimitiveInspector.DrawInput(serialized);
            serialized.ApplyModifiedProperties();
            PrimitiveInspector.DrawDiagnostics(serialized, ref _showDiagnostics, GetMeshWarning());
        }

        private static void DrawShape(SerializedObject serialized)
        {
            PrimitiveInspector.Field(serialized, "_keepSquare");
            var individualCorners = serialized.FindProperty("_separateRoundingForCorners");
            EditorGUILayout.PropertyField(individualCorners);
            if (individualCorners.hasMultipleDifferentValues || !individualCorners.boolValue)
            {
                PrimitiveInspector.Field(serialized, "_cornerRadius");
            }

            if (PrimitiveInspector.CanBeEnabled(individualCorners))
            {
                PrimitiveInspector.Field(serialized, "_cornersRoundings", "Corner Radii");
            }

            PrimitiveInspector.Count(serialized.FindProperty("_cornerQuality"), "Corner Segments", 3, 36);
        }

        private string GetMeshWarning()
        {
            foreach (var selected in targets)
            {
                if (selected is RoundedRectGraphic graphic && graphic.DashBudgetExceeded)
                {
                    return "The dashed outline exceeds the mesh vertex limit. Increase the dash or gap length.";
                }
            }

            return null;
        }
    }
}
