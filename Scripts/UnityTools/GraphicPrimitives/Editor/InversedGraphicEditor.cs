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
        private static readonly GUIContent EditHoleLabel = new("Edit Hole",
            "Move the center and drag the side handles to resize the hole in the Scene view.");

        private readonly PathEditSession _holeEdit = new();
        private bool _showDiagnostics;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _holeEdit.SetTarget(null);
        }

        public override void OnInspectorGUI()
        {
            var serialized = serializedObject;
            serialized.Update();
            var graphic = targets.Length == 1 ? (InversedGraphic)target : null;

            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serialized);
            DrawShape(serialized);
            DrawEditHoleToggle(graphic);

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
            if (PrimitiveInspector.CanBeEnabled(serialized.FindProperty("m_RaycastTarget")))
            {
                PrimitiveInspector.Field(serialized, "_raycastThroughHole", "Raycast Through Hole");
            }

            serialized.ApplyModifiedProperties();

            PrimitiveInspector.DrawDiagnostics(ref _showDiagnostics, graphic?.VertexCount, graphic?.TriangleCount, GetMeshWarning());
        }

        private void DrawEditHoleToggle(InversedGraphic graphic)
        {
            bool editing = _holeEdit.IsEditing(graphic);
            using var disabled = new EditorGUI.DisabledScope(graphic == null);
            bool next = GUILayout.Toggle(editing, EditHoleLabel, GUI.skin.button);
            if (graphic == null)
            {
                _holeEdit.SetTarget(null);
            }
            else if (editing != next)
            {
                _holeEdit.SetTarget(next ? graphic : null);
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            var graphic = (InversedGraphic)target;
            if (!_holeEdit.IsEditing(graphic) || !PathHandlePlane.TryCreate(graphic.rectTransform, out var plane))
            {
                return;
            }

            var panel = graphic.GetPixelAdjustedRect();
            if (panel.width <= 0f || panel.height <= 0f)
            {
                return;
            }

            var rectTransform = graphic.rectTransform;
            var hole = graphic.GetHole(panel);
            var center = rectTransform.TransformPoint(hole.center);
            var displaySize = HandleUtility.GetHandleSize(rectTransform.TransformPoint(panel.center));

            using var drawing = new Handles.DrawingScope(PathHandlePlane.PathColor);
            DrawHoleBounds(rectTransform, hole);
            var moved = plane.DrawPoint(center, true, out _);
            var minDelta = new Vector2(DrawSizeHandle(rectTransform, hole, 0, false, displaySize),
                DrawSizeHandle(rectTransform, hole, 1, false, displaySize));
            var maxDelta = new Vector2(DrawSizeHandle(rectTransform, hole, 0, true, displaySize),
                DrawSizeHandle(rectTransform, hole, 1, true, displaySize));

            if (moved != center)
            {
                var half = hole.size * 0.5f;
                var local = (Vector2)rectTransform.InverseTransformPoint(moved);
                var clamped = new Vector2(Mathf.Clamp(local.x, panel.xMin + half.x, panel.xMax - half.x),
                    Mathf.Clamp(local.y, panel.yMin + half.y, panel.yMax - half.y));
                if (clamped != hole.center)
                {
                    ApplySceneHole(panel, clamped, null);
                }
            }
            else if (minDelta != Vector2.zero || maxDelta != Vector2.zero)
            {
                int axis = minDelta.x != 0f || maxDelta.x != 0f ? 0 : 1;
                bool positive = maxDelta[axis] != 0f;
                float delta = positive ? maxDelta[axis] : minDelta[axis];
                var resized = ResizeHole(panel, hole, graphic.LockHoleAspect, axis, positive, delta);
                if (resized.size != hole.size)
                {
                    ApplySceneHole(panel, resized.center, resized.size, graphic.LockHoleAspect ? -1 : axis);
                }
            }

            if (GUIUtility.hotControl == 0)
            {
                _holeEdit.EndDrag();
            }
        }

        private static void DrawHoleBounds(RectTransform rectTransform, Rect hole)
        {
            var bottomLeft = rectTransform.TransformPoint(hole.min);
            var bottomRight = rectTransform.TransformPoint(new Vector2(hole.xMax, hole.yMin));
            var topRight = rectTransform.TransformPoint(hole.max);
            var topLeft = rectTransform.TransformPoint(new Vector2(hole.xMin, hole.yMax));
            Handles.DrawLine(bottomLeft, bottomRight);
            Handles.DrawLine(bottomRight, topRight);
            Handles.DrawLine(topRight, topLeft);
            Handles.DrawLine(topLeft, bottomLeft);
        }

        private static float DrawSizeHandle(RectTransform rectTransform, Rect hole, int axis, bool positive, float displaySize)
        {
            var edge = hole.center;
            edge[axis] = positive ? hole.max[axis] : hole.min[axis];
            var position = rectTransform.TransformPoint(edge);
            var localAxis = axis == 0 ? Vector3.right : Vector3.up;
            var direction = rectTransform.TransformVector(positive ? localAxis : -localAxis).normalized;
            var grip = position + direction * displaySize * 0.14f;
            Handles.DrawLine(position, grip);
            var moved = Handles.Slider(grip, direction, displaySize * 0.055f, Handles.DotHandleCap, 0f);
            return rectTransform.InverseTransformVector(moved - grip)[axis];
        }

        private static Rect ResizeHole(Rect panel, Rect hole, bool lockAspect, int axis, bool positive, float delta)
        {
            int perpendicular = 1 - axis;
            float fixedEdge = positive ? hole.min[axis] : hole.max[axis];
            float movingEdge = positive ? hole.max[axis] : hole.min[axis];
            float available = positive ? panel.max[axis] - fixedEdge : fixedEdge - panel.min[axis];
            if (lockAspect)
            {
                available = Mathf.Min(available, panel.size[perpendicular]);
            }

            float direction = positive ? 1f : -1f;
            float extent = Mathf.Clamp((movingEdge + delta - fixedEdge) * direction, 0f, available);
            var center = hole.center;
            var size = hole.size;
            center[axis] = fixedEdge + direction * extent * 0.5f;
            size[axis] = extent;
            if (lockAspect)
            {
                size[perpendicular] = extent;
                float half = extent * 0.5f;
                center[perpendicular] = Mathf.Clamp(center[perpendicular], panel.min[perpendicular] + half,
                    panel.max[perpendicular] - half);
            }

            return new Rect(center - size * 0.5f, size);
        }

        private void ApplySceneHole(Rect panel, Vector2 center, Vector2? size, int sizeAxis = -1)
        {
            var edit = _holeEdit.ScenePath;
            edit.UpdateIfRequiredOrScript();
            _holeEdit.BeginDrag(size.HasValue ? "Resize Hole" : "Move Hole");
            var offset = center - panel.min;
            edit.FindProperty("_holeCenter").vector2Value = new Vector2(Mathf.Clamp01(offset.x / panel.width),
                Mathf.Clamp01(offset.y / panel.height));
            if (size.HasValue)
            {
                var value = size.Value;
                var sizeProperty = edit.FindProperty("_holeSize");
                var normalized = sizeProperty.vector2Value;
                if (sizeAxis != 1)
                {
                    normalized.x = Mathf.Clamp01(value.x / panel.width);
                }

                if (sizeAxis != 0)
                {
                    normalized.y = Mathf.Clamp01(value.y / panel.height);
                }

                sizeProperty.vector2Value = normalized;
            }

            if (edit.ApplyModifiedProperties())
            {
                Repaint();
                SceneView.RepaintAll();
            }
        }

        private void OnUndoRedo()
        {
            _holeEdit.OnUndoRedo();
            Repaint();
            SceneView.RepaintAll();
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
