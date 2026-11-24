using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(SunburstGraphic)), CanEditMultipleObjects]
    public class SunburstGraphicEditor : Editor
    {
        private bool _diagnostics;

        public override void OnInspectorGUI()
        {
            var serialized = serializedObject;
            serialized.Update();

            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serialized);
            PrimitiveInspector.Field(serialized, "_profile");
            PrimitiveInspector.Field(serialized, "_startAngle", "Start Angle (°)");
            PrimitiveInspector.Field(serialized, "_keepCircle");
            PrimitiveInspector.Count(serialized.FindProperty("_peaksCount"), "Peaks", 3);
            PrimitiveInspector.Field(serialized, "_outerDisplacementFactor", "Tip Radius");
            PrimitiveInspector.Field(serialized, "_innerDisplacementFactor", "Valley Radius");
            var profile = serialized.FindProperty("_profile");
            if (profile.hasMultipleDifferentValues || (SunburstGraphic.ProfileType)profile.intValue == SunburstGraphic.ProfileType.Smooth)
            {
                PrimitiveInspector.Count(serialized.FindProperty("_curvesSmoothness"), "Samples per Curve", 2, 80);
                PrimitiveInspector.Field(serialized, "_innerTangentPointShift", "Valley Tangent Radial Offset");
                PrimitiveInspector.Field(serialized, "_innerTangentPointShiftPerp", "Valley Tangent Side Offset");
                PrimitiveInspector.Field(serialized, "_outerTangentPointShift", "Tip Tangent Radial Offset");
                PrimitiveInspector.Field(serialized, "_outerTangentPointShiftPerp", "Tip Tangent Side Offset");
            }

            var drawFill = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Edge;
            var drawStroke = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Fill;
            if (drawFill)
            {
                PrimitiveInspector.DrawFill(serialized);
            }

            if (drawStroke)
            {
                PrimitiveInspector.DrawStroke(serialized, thicknessMode: "_edgeThicknessMode");
                PrimitiveInspector.DrawPerimeterDashes(serialized, "_dashLength", "_gapLength", "_dashPhase");
            }

            PrimitiveInspector.DrawAntiAliasing(serialized, "_antiAliasingLayerWidth",
                drawStroke ? "_innerAntiAliasingLayerWidth" : null);
            PrimitiveInspector.DrawRendering(serialized);
            PrimitiveInspector.DrawInput(serialized);
            serialized.ApplyModifiedProperties();

            var graphic = targets.Length == 1 ? (SunburstGraphic)target : null;
            PrimitiveInspector.DrawDiagnostics(ref _diagnostics, graphic?.VertexCount, graphic?.TriangleCount, GetMeshWarning());
        }

        private string GetMeshWarning()
        {
            foreach (var item in targets)
            {
                var graphic = (SunburstGraphic)item;
                if (graphic.MeshBudgetExceeded)
                {
                    return "The outline exceeds the mesh budget. Reduce the peak count or use a larger dash cycle.";
                }
            }

            return null;
        }
    }
}
