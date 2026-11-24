using UnityEditor;
using UnityEngine;

namespace Tools.EditorOnly.GraphicPrimitives
{
    internal static class PrimitiveInspector
    {
        private static readonly GUIContent ThicknessLabel = new("Thickness", "Stroke thickness in local UI units.");
        private static readonly GUIContent RelativeThicknessLabel = new("Relative Thickness (%)",
            "Stroke thickness as a percentage of half the shorter side.");
        private static readonly GUIContent OuterWidthLabel = new("Outer Width", "Width of the fade at the outer edge, in local UI units.");
        private static readonly GUIContent InnerWidthLabel = new("Inner Width", "Width of the fade at the inner edge of the stroke, in local UI units.");
        private static readonly GUIContent DashLengthLabel = new("Dash Length",
            "Desired dash length in local UI units. The pattern fits evenly around the perimeter.");
        private static readonly GUIContent GapLengthLabel = new("Gap Length",
            "Desired gap length in local UI units. The pattern fits evenly around the perimeter.");
        private static readonly GUIContent PhaseLabel = new("Phase", "Offset as a fraction of one dash and gap cycle.");

        internal static void Section(string name)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
        }

        internal static void Field(SerializedObject serialized, string name, string label = null)
        {
            if (name == null)
            {
                return;
            }

            Field(serialized.FindProperty(name), label);
        }

        internal static void Field(SerializedProperty property, string label)
        {
            if (property == null)
            {
                return;
            }

            if (label == null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
            else
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        internal static void Number(SerializedProperty property, GUIContent label, float minimum = 0f, float maximum = float.MaxValue)
        {
            var rect = EditorGUILayout.GetControlRect();
            using var scope = new EditorGUI.PropertyScope(rect, label, property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.FloatField(rect, scope.content, property.floatValue);
            if (EditorGUI.EndChangeCheck() && float.IsFinite(value))
            {
                property.floatValue = Mathf.Clamp(value, minimum, maximum);
            }
        }

        internal static void Count(SerializedProperty property, string label, int minimum, int maximum = int.MaxValue, string tooltip = null)
        {
            var rect = EditorGUILayout.GetControlRect();
            using var scope = new EditorGUI.PropertyScope(rect, new GUIContent(label, tooltip ?? property.tooltip), property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.IntField(rect, scope.content, property.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = Mathf.Clamp(value, minimum, maximum);
            }
        }

        private static void DrawRelativeThickness(SerializedProperty property, float relativeThicknessScale)
        {
            var rect = EditorGUILayout.GetControlRect();
            using var scope = new EditorGUI.PropertyScope(rect, RelativeThicknessLabel, property);
            var percentPerUnit = 100f * relativeThicknessScale;
            var percent = Mathf.Clamp(property.floatValue * percentPerUnit, 0f, 100f);
            EditorGUI.BeginChangeCheck();
            percent = EditorGUI.Slider(rect, scope.content, percent, 0f, 100f);
            if (EditorGUI.EndChangeCheck() && float.IsFinite(percent))
            {
                property.floatValue = percent / percentPerUnit;
            }
        }

        internal static bool CanBeEnabled(SerializedProperty property)
        {
            return property != null && (property.hasMultipleDifferentValues || property.boolValue);
        }

        internal static SerializedProperty DrawMode(SerializedObject serialized, string propertyName = "_mode")
        {
            return DrawMode(serialized.FindProperty(propertyName));
        }

        internal static SerializedProperty DrawMode(SerializedProperty mode)
        {
            Field(mode, "Draw Mode");
            return mode;
        }

        internal static void DrawFill(SerializedObject serialized)
        {
            Section("Fill");
            DrawPaint(serialized, "m_Color", "_shouldUseGradient", "_gradientType", "_gradientColor");
        }

        internal static void DrawStroke(SerializedObject serialized, string thickness = "_edgeThickness",
            string thicknessMode = null, string color = "_edgeColor", string gradientEnabled = "_shouldUseGradientForEdge",
            string gradient = "_edgeColorGradient", float relativeThicknessScale = 1f)
        {
            Section("Stroke");
            var mode = thicknessMode == null ? null : serialized.FindProperty(thicknessMode);
            Field(mode, "Thickness Mode");
            if (mode == null || mode.hasMultipleDifferentValues || mode.intValue == 0)
            {
                Number(serialized.FindProperty(thickness), ThicknessLabel);
            }

            if (mode != null && (mode.hasMultipleDifferentValues || mode.intValue == 1))
            {
                DrawRelativeThickness(serialized.FindProperty("_edgeThicknessRelative"), relativeThicknessScale);
            }

            DrawPaint(serialized, color, gradientEnabled, "_edgeGradientType", gradient);
        }

        private static void DrawPaint(SerializedObject serialized, string color, string gradientEnabled,
            string gradientDirection, string gradient)
        {
            var enabled = gradientEnabled == null ? null : serialized.FindProperty(gradientEnabled);
            Field(enabled, "Use Gradient");
            if (CanBeEnabled(enabled))
            {
                Field(serialized, gradientDirection, "Gradient Direction");
                Field(serialized, gradient, "Gradient");
            }

            if (enabled == null || enabled.hasMultipleDifferentValues || !enabled.boolValue)
            {
                Field(serialized, color, "Color");
            }
        }

        internal static bool DrawDashToggle(SerializedObject serialized, string propertyName = "_isDashed")
        {
            return DrawDashToggle(serialized.FindProperty(propertyName));
        }

        internal static bool DrawDashToggle(SerializedProperty property)
        {
            Field(property, "Dashed");
            return CanBeEnabled(property);
        }

        internal static void DrawPerimeterDashes(SerializedObject serialized, string length, string gap, string phase)
        {
            if (!DrawDashToggle(serialized))
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                Number(serialized.FindProperty(length), DashLengthLabel);
                Number(serialized.FindProperty(gap), GapLengthLabel);
                EditorGUILayout.Slider(serialized.FindProperty(phase), 0f, 1f, PhaseLabel);
            }
        }

        internal static void DrawAntiAliasing(SerializedObject serialized, string outerWidth, string innerWidth = null,
            float outerMaximum = float.MaxValue)
        {
            Section("Anti-Aliasing");
            Number(serialized.FindProperty(outerWidth), OuterWidthLabel, maximum: outerMaximum);
            if (innerWidth != null)
            {
                Number(serialized.FindProperty(innerWidth), InnerWidthLabel);
            }
        }

        internal static void DrawRendering(SerializedObject serialized)
        {
            Section("Rendering");
            Field(serialized, "m_Sprite");
            Field(serialized, "m_Material");
            Field(serialized, "m_Maskable");
            DrawStencil(serialized);
        }

        internal static void DrawInput(SerializedObject serialized)
        {
            Section("Input");
            Field(serialized, "m_RaycastTarget");
            if (CanBeEnabled(serialized.FindProperty("m_RaycastTarget")))
            {
                Field(serialized, "m_RaycastPadding");
            }
        }

        internal static void DrawDiagnostics(SerializedObject serialized, ref bool expanded, string warning = null)
        {
            var vertices = serialized.FindProperty("_vertCount");
            var triangles = serialized.FindProperty("_trianglesCount");
            if ((vertices != null || triangles != null) && DrawDiagnosticsFoldout(ref expanded))
            {
                DrawDiagnosticCount(vertices, "Vertices");
                DrawDiagnosticCount(triangles, "Triangles");
            }

            DrawWarning(warning);
        }

        private static void DrawDiagnosticCount(SerializedProperty property, string label)
        {
            if (property != null)
            {
                EditorGUILayout.LabelField(label, property.hasMultipleDifferentValues ? "—" : property.intValue.ToString());
            }
        }

        internal static void DrawDiagnostics(ref bool expanded, int? vertexCount, int? triangleCount, string warning = null)
        {
            if (vertexCount.HasValue && triangleCount.HasValue && DrawDiagnosticsFoldout(ref expanded))
            {
                EditorGUILayout.LabelField("Vertices", vertexCount.Value.ToString());
                EditorGUILayout.LabelField("Triangles", triangleCount.Value.ToString());
            }

            DrawWarning(warning);
        }

        private static bool DrawDiagnosticsFoldout(ref bool expanded)
        {
            EditorGUILayout.Space();
            expanded = EditorGUILayout.Foldout(expanded, "Mesh Diagnostics", true);
            return expanded;
        }

        private static void DrawWarning(string warning)
        {
            if (!string.IsNullOrEmpty(warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
        }

        internal static void DrawStencil(SerializedObject serialized)
        {
            var stencil = serialized.FindProperty("_overrideStencil");
            if (stencil == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(stencil);
            if (!CanBeEnabled(stencil))
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                Field(serialized, "_stencilComp", "Comparison");
                Field(serialized, "_stencilRef", "Reference");
                Field(serialized, "_stencilOp", "Pass Operation");
                Field(serialized, "_overrideStencilOperation", "Override Inherited Operation");
                Field(serialized, "_hideMaskGraphics", "Hide Graphics");
                var masks = serialized.FindProperty("_overrideStencilMasks");
                EditorGUILayout.PropertyField(masks);
                if (CanBeEnabled(masks))
                {
                    Field(serialized, "_stencilReadMask", "Read Mask");
                    Field(serialized, "_stencilWriteMask", "Write Mask");
                }
            }
        }
    }
}
