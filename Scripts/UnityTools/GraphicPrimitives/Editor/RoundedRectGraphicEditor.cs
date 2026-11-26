using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    using EdgeType = RoundedRectGraphic.EdgeType;

    [CustomEditor(typeof(RoundedRectGraphic)), CanEditMultipleObjects]
    public class RoundedRectGraphicEditor : Editor
    {
        private static readonly GUIContent DashLengthLabel = new("Dash Length", "Desired dash length in local UI units.");
        private static readonly GUIContent GapLengthLabel = new("Gap Length", "Desired gap between dashes in local UI units.");
        private static readonly GUIContent PhaseLabel = new("Phase",
            "Offset as a fraction of one dash and gap cycle. Corner alignment sets this automatically.");

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
                DrawDashes(serialized);
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

        private static void DrawDashes(SerializedObject serialized)
        {
            var dashed = serialized.FindProperty("_isDashed");
            if (!PrimitiveInspector.DrawDashToggle(dashed))
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(dashed.hasMultipleDifferentValues))
            {
                var alignToCorners = serialized.FindProperty("_alignDashesToCorners");
                PrimitiveInspector.Field(alignToCorners, "Align Dashes To Corners");
                PrimitiveInspector.Number(serialized.FindProperty("_dashLengthPixels"), DashLengthLabel);
                PrimitiveInspector.Number(serialized.FindProperty("_dashGapLengthPixels"), GapLengthLabel);
                using (new EditorGUI.DisabledScope(alignToCorners.hasMultipleDifferentValues || alignToCorners.boolValue))
                {
                    EditorGUILayout.Slider(serialized.FindProperty("_dashShift"), 0f, 1f, PhaseLabel);
                }
            }
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
