using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    internal static class ClosedShapeInspector
    {
        internal static void DrawAppearance(SerializedProperty mode, string outerAa)
        {
            var serialized = mode.serializedObject;
            var drawFill = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Edge;
            var drawStroke = mode.hasMultipleDifferentValues || (ClosedShapeMode)mode.intValue != ClosedShapeMode.Fill;
            if (drawFill)
            {
                PrimitiveInspector.DrawFill(serialized);
            }

            if (drawStroke)
            {
                PrimitiveInspector.DrawStroke(serialized, gradientEnabled: "_shouldUseEdgeGradient", gradient: "_edgeGradient");
            }

            PrimitiveInspector.DrawAntiAliasing(serialized, outerAa, drawStroke ? "_innerAntiAliasingLayerWidth" : null);
            PrimitiveInspector.DrawRendering(serialized);
            PrimitiveInspector.DrawInput(serialized);
        }
    }
}
