using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(ArrowGraphic)), CanEditMultipleObjects]
    public class ArrowGraphicEditor : Editor
    {
        private const int MinimumKnots = 2;
        private const string TangentPresenceField = "_hasValue";
        private const string TangentPositionField = "_value";
        private static readonly GUIContent InsertPointLabel = new("Insert Point", "Insert a point between the selected point and its neighbor.");
        private static readonly GUIContent EndingLengthLabel = new("Length", "Length of the arrowhead in local UI units.");

        private readonly PathEditSession _pathEdit = new();
        private bool _showDiagnostics;
        private int _selectedKnot;
        private Vector3[] _previewPoints = System.Array.Empty<Vector3>();

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _pathEdit.SetTarget(null);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();
            var settings = serializedObject.FindProperty("_settings");
            var path = settings.FindPropertyRelative("KnotsConfig");
            var useSpline = path.FindPropertyRelative("UseSpline");
            var knots = path.FindPropertyRelative("Knots");

            PrimitiveInspector.Section("Shape");
            EditorGUILayout.PropertyField(useSpline, new GUIContent("Bézier"));
            PrimitiveInspector.Count(settings.FindPropertyRelative("SegmentsNumber"), "Samples per Span", 1, 1000,
                "Minimum Bézier resolution per span and the segment unit for the dash pattern.");
            if (targets.Length == 1 && target is ArrowGraphic arrow)
            {
                DrawPathInspector(arrow, knots, useSpline.boolValue);
            }
            else
            {
                _pathEdit.SetTarget(null);
                EditorGUILayout.HelpBox("Select one arrow to edit its path points.", MessageType.Info);
            }

            PrimitiveInspector.DrawStroke(serializedObject, thickness: "_settings.Thickness", color: "m_Color", gradientEnabled: null);
            var rounded = settings.FindPropertyRelative("IsRounded");
            PrimitiveInspector.Field(rounded, "Round Caps");
            var endingsOnly = settings.FindPropertyRelative("IsEndingsOnly");
            PrimitiveInspector.Field(endingsOnly, "Endings Only");
            if (endingsOnly.hasMultipleDifferentValues || !endingsOnly.boolValue)
            {
                DrawDashes(settings.FindPropertyRelative("DashConfig"));
            }

            EditorGUILayout.LabelField("Endings", EditorStyles.miniBoldLabel);
            DrawEnding(settings.FindPropertyRelative("StartEnding"), "Start", rounded);
            DrawEnding(settings.FindPropertyRelative("EndEnding"), "End", rounded);

            PrimitiveInspector.DrawAntiAliasing(serializedObject, "_settings.AntiAliasingLayerWidth", outerMaximum: 100f);
            PrimitiveInspector.DrawRendering(serializedObject);
            PrimitiveInspector.DrawInput(serializedObject);

            if (targets.Length == 1 && target is ArrowGraphic selected)
            {
                var info = selected.MeshInfoAccess;
                PrimitiveInspector.DrawDiagnostics(ref _showDiagnostics, info.Vertices, info.Triangles);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }
        }

        private static void DrawDashes(SerializedProperty dash)
        {
            var enabled = dash.FindPropertyRelative("DashesEnabled");
            if (!PrimitiveInspector.DrawDashToggle(enabled))
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(enabled.hasMultipleDifferentValues))
            {
                PrimitiveInspector.Count(dash.FindPropertyRelative("DashLengthInSegments"), "Dash Length (Segments)", 1);
                PrimitiveInspector.Count(dash.FindPropertyRelative("GapLengthInSegments"), "Gap Length (Segments)", 1);
                PrimitiveInspector.Field(dash.FindPropertyRelative("OffsetInSegments"), "Offset (Segments)");
            }
        }

        private static void DrawEnding(SerializedProperty ending, string label, SerializedProperty rounded)
        {
            var type = ending.FindPropertyRelative("Type");
            EditorGUILayout.PropertyField(type, new GUIContent(label));
            if (!type.hasMultipleDifferentValues && (ArrowGraphic.EndingType)type.intValue == ArrowGraphic.EndingType.None)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(type.hasMultipleDifferentValues))
            {
                EditorGUILayout.PropertyField(ending.FindPropertyRelative("Angle"), new GUIContent("Angle (°)"));
                PrimitiveInspector.Number(ending.FindPropertyRelative("Length"), EndingLengthLabel, minimum: 1f);
                if ((ArrowGraphic.EndingType)type.intValue == ArrowGraphic.EndingType.Arrow && PrimitiveInspector.CanBeEnabled(rounded))
                {
                    PrimitiveInspector.Count(ending.FindPropertyRelative("RoundedArrowSmoothness"), "Round Tip Samples", 1, 100);
                }
            }
        }

        private void DrawPathInspector(ArrowGraphic arrow, SerializedProperty knots, bool useSpline)
        {
            _selectedKnot = Mathf.Clamp(_selectedKnot, 0, Mathf.Max(0, knots.arraySize - 1));
            var wasEditingPath = _pathEdit.IsEditing(arrow);
            var editPath = BezierPathInspector.DrawEditPathToggle(wasEditingPath);
            if (wasEditingPath != editPath)
            {
                _pathEdit.SetTarget(editPath ? arrow : null);
                SceneView.RepaintAll();
            }

            var action = BezierPathInspector.DrawPointButtons(knots.arraySize, MinimumKnots, ArrowGraphic.MaximumPathPoints, InsertPointLabel);
            if (action == BezierPathInspector.PointAction.Insert)
            {
                InsertPoint(arrow, knots, useSpline);
            }
            else if (action == BezierPathInspector.PointAction.Remove)
            {
                knots.DeleteArrayElementAtIndex(_selectedKnot);
                _selectedKnot = Mathf.Min(_selectedKnot, knots.arraySize - 1);
            }

            if (knots.arraySize == 0)
            {
                return;
            }

            _selectedKnot = BezierPathInspector.DrawPointSelection(_selectedKnot, knots.arraySize);

            var knot = knots.GetArrayElementAtIndex(_selectedKnot);
            var position = knot.FindPropertyRelative("Position");
            var previousPosition = position.vector2Value;
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(position, new GUIContent("Position", "Coordinates in the reference RectTransform size."));
                if (change.changed)
                {
                    EnsurePathReference(arrow, knots);
                    TranslateManualTangents(knot, position.vector2Value - previousPosition);
                }
            }

            if (!useSpline)
            {
                return;
            }

            var mode = knot.FindPropertyRelative(nameof(ArrowGraphic.LineKnot.Mode));
            var hasTwoTangents = IsInteriorKnot(knots, _selectedKnot);
            if (BezierPathInspector.DrawTangentMode(mode, hasTwoTangents))
            {
                ApplyTangentMode(arrow, knots, knot);
            }

            var linked = hasTwoTangents && (BezierTangentMode)mode.intValue != BezierTangentMode.Broken;
            if (_selectedKnot > 0)
            {
                DrawInspectorTangent(arrow, knots, true, linked);
            }

            if (_selectedKnot < knots.arraySize - 1)
            {
                DrawInspectorTangent(arrow, knots, false, linked);
            }
        }

        private void DrawInspectorTangent(ArrowGraphic arrow, SerializedProperty knots, bool incoming, bool linked)
        {
            var knot = knots.GetArrayElementAtIndex(_selectedKnot);
            var tangent = GetTangentProperty(knot, incoming);
            var hasValue = tangent.FindPropertyRelative(TangentPresenceField);
            var label = incoming ? "Incoming Tangent" : "Outgoing Tangent";
            if (!linked)
            {
                using var change = new EditorGUI.ChangeCheckScope();
                EditorGUILayout.PropertyField(hasValue, new GUIContent($"Set {label}"));
                if (change.changed && hasValue.boolValue)
                {
                    // Resolve the previous automatic value before enabling this control point.
                    hasValue.boolValue = false;
                    var automatic = GetAuthoredTangent(knots, _selectedKnot, incoming);
                    WriteTangent(tangent, automatic);
                    EnsurePathReference(arrow, knots);
                }
            }

            if (hasValue.boolValue || linked)
            {
                using var change = new EditorGUI.ChangeCheckScope();
                var value = tangent.FindPropertyRelative(TangentPositionField);
                EditorGUILayout.PropertyField(value, new GUIContent(label));
                if (change.changed)
                {
                    EnsurePathReference(arrow, knots);
                    SetAuthoredTangent(knots, _selectedKnot, incoming, value.vector2Value);
                }
            }
        }

        private void InsertPoint(ArrowGraphic arrow, SerializedProperty knots, bool useSpline)
        {
            EnsurePathReference(arrow, knots);
            if (knots.arraySize == 0)
            {
                knots.arraySize = MinimumKnots;
                var rect = arrow.GetPathRect();
                var center = ArrowGraphic.IsUsableRect(rect) ? rect.center : Vector2.zero;
                var offset = ArrowGraphic.IsUsableRect(rect) ? rect.width * 0.25f : 30f;
                WriteKnot(knots.GetArrayElementAtIndex(0), arrow.ToAuthoredPathPoint(center - Vector2.right * offset, rect));
                WriteKnot(knots.GetArrayElementAtIndex(1), arrow.ToAuthoredPathPoint(center + Vector2.right * offset, rect));
                _selectedKnot = 1;
                return;
            }

            if (knots.arraySize == 1)
            {
                var position = knots.GetArrayElementAtIndex(0).FindPropertyRelative("Position").vector2Value;
                knots.arraySize = MinimumKnots;
                WriteKnot(knots.GetArrayElementAtIndex(1), position + Vector2.right * 60f);
                _selectedKnot = 1;
                return;
            }

            // Select the following span, or the preceding one at the last point.
            var firstIndex = Mathf.Min(_selectedKnot, knots.arraySize - 2);
            var insertIndex = firstIndex + 1;
            var first = ReadKnot(knots.GetArrayElementAtIndex(firstIndex));
            var last = ReadKnot(knots.GetArrayElementAtIndex(insertIndex));
            knots.InsertArrayElementAtIndex(insertIndex);
            var inserted = knots.GetArrayElementAtIndex(insertIndex);

            if (useSpline)
            {
                var curve = ArrowGraphic.GetAuthoredCurve(in first, in last);
                curve.Split(0.5f, out var before, out var after);
                WriteKnot(inserted, before.End);
                WriteTangent(GetTangentProperty(inserted, true), before.Incoming);
                WriteTangent(GetTangentProperty(inserted, false), after.Outgoing);
                SetSplitTangent(knots, firstIndex, false, before.Outgoing);
                SetSplitTangent(knots, insertIndex + 1, true, after.Incoming);
            }
            else
            {
                WriteKnot(inserted, Vector2.Lerp(first.Position, last.Position, 0.5f));
            }

            _selectedKnot = insertIndex;
        }

        private void OnSceneGUI()
        {
            if (target is not ArrowGraphic arrow || !_pathEdit.IsEditing(arrow))
            {
                return;
            }

            var scenePath = _pathEdit.ScenePath;
            scenePath.UpdateIfRequiredOrScript();
            var path = scenePath.FindProperty("_settings").FindPropertyRelative("KnotsConfig");
            var knots = path.FindPropertyRelative("Knots");
            var knotCount = knots.arraySize;
            var rect = arrow.GetPathRect();
            if (knotCount == 0 || knotCount > ArrowGraphic.MaximumPathPoints || !ArrowGraphic.IsUsableRect(rect))
            {
                _pathEdit.EndDrag();
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                DrawPathPreview(arrow);
            }

            _selectedKnot = Mathf.Clamp(_selectedKnot, 0, knotCount - 1);
            var tangentKnot = _selectedKnot;
            var reference = arrow.rectTransform;
            if (!PathHandlePlane.TryCreate(reference, out var plane))
            {
                _pathEdit.EndDrag();
                return;
            }

            for (var i = 0; i < knotCount; i++)
            {
                var knot = knots.GetArrayElementAtIndex(i);
                var position = knot.FindPropertyRelative("Position");
                var previous = position.vector2Value;
                var local = arrow.ToLocalPathPoint(previous, rect);
                if (!ArrowGraphic.IsFinite(local))
                {
                    continue;
                }

                var world = reference.TransformPoint(local);
                var next = plane.DrawPoint(world, i == _selectedKnot, out var selectPoint);
                if (selectPoint)
                {
                    _selectedKnot = i;
                    Repaint();
                }

                if (next != world)
                {
                    _pathEdit.BeginDrag("Edit Arrow Path");
                    EnsurePathReference(arrow, knots);
                    var authored = arrow.ToAuthoredPathPoint(reference.InverseTransformPoint(next), rect);
                    position.vector2Value = authored;
                    TranslateManualTangents(knot, authored - previous);
                }
            }

            if (path.FindPropertyRelative("UseSpline").boolValue)
            {
                if (tangentKnot > 0)
                {
                    DrawSceneTangent(arrow, knots, tangentKnot, true, rect, in plane);
                }

                if (tangentKnot < knotCount - 1)
                {
                    DrawSceneTangent(arrow, knots, tangentKnot, false, rect, in plane);
                }
            }

            if (scenePath.ApplyModifiedProperties())
            {
                Repaint();
                SceneView.RepaintAll();
            }

            if (GUIUtility.hotControl == 0)
            {
                _pathEdit.EndDrag();
            }
        }

        private void DrawPathPreview(ArrowGraphic arrow)
        {
            var points = arrow.GetPathPreview();
            if (points.Count < 2)
            {
                return;
            }

            if (_previewPoints.Length != points.Count)
            {
                _previewPoints = new Vector3[points.Count];
            }

            for (var i = 0; i < points.Count; i++)
            {
                _previewPoints[i] = arrow.rectTransform.TransformPoint(points[i].Position);
            }

            using var drawing = new Handles.DrawingScope(PathHandlePlane.PathColor);
            Handles.DrawAAPolyLine(2f, _previewPoints);
        }

        private void DrawSceneTangent(
            ArrowGraphic arrow, SerializedProperty knots, int index, bool incoming, Rect rect, in PathHandlePlane plane)
        {
            var knot = knots.GetArrayElementAtIndex(index);
            var position = knot.FindPropertyRelative("Position").vector2Value;
            var authored = GetAuthoredTangent(knots, index, incoming);
            var reference = arrow.rectTransform;
            var localAnchor = arrow.ToLocalPathPoint(position, rect);
            var localTangent = arrow.ToLocalPathPoint(authored, rect);
            if (!ArrowGraphic.IsFinite(localAnchor) || !ArrowGraphic.IsFinite(localTangent))
            {
                return;
            }

            var anchor = reference.TransformPoint(localAnchor);
            var world = reference.TransformPoint(localTangent);
            var next = plane.DrawTangent(anchor, world);
            if (next != world)
            {
                _pathEdit.BeginDrag("Edit Arrow Path");
                EnsurePathReference(arrow, knots);
                SetAuthoredTangent(knots, index, incoming, arrow.ToAuthoredPathPoint(reference.InverseTransformPoint(next), rect));
            }
        }

        private void ApplyTangentMode(ArrowGraphic arrow, SerializedProperty knots, SerializedProperty knot)
        {
            var mode = (BezierTangentMode)knot.FindPropertyRelative(nameof(ArrowGraphic.LineKnot.Mode)).intValue;
            if (mode == BezierTangentMode.Broken)
            {
                return;
            }

            var point = ReadKnot(knot);
            var outgoing = GetAuthoredTangent(knots, _selectedKnot, false);
            var incoming = GetAuthoredTangent(knots, _selectedKnot, true);
            var anchorIncoming = (point.PrevTangentPoint.HasValue && !point.NextTangentPoint.HasValue) ||
                (outgoing == point.Position && incoming != point.Position);
            EnsurePathReference(arrow, knots);
            SetAuthoredTangent(knots, _selectedKnot, anchorIncoming, anchorIncoming ? incoming : outgoing);
        }

        private static void SetAuthoredTangent(SerializedProperty knots, int index, bool incoming, Vector2 position)
        {
            var knot = knots.GetArrayElementAtIndex(index);
            var mode = (BezierTangentMode)knot.FindPropertyRelative(nameof(ArrowGraphic.LineKnot.Mode)).intValue;
            WriteTangent(GetTangentProperty(knot, incoming), position);
            if (mode != BezierTangentMode.Broken && IsInteriorKnot(knots, index))
            {
                var anchor = knot.FindPropertyRelative("Position").vector2Value;
                var opposite = GetAuthoredTangent(knots, index, !incoming);
                var linked = BezierPath.GetOppositeTangent(mode, anchor, position, opposite);
                WriteTangent(GetTangentProperty(knot, !incoming), linked);
            }
        }

        private static void SetSplitTangent(SerializedProperty knots, int index, bool incoming, Vector2 position)
        {
            var knot = knots.GetArrayElementAtIndex(index);
            WriteTangent(GetTangentProperty(knot, incoming), position);

            var mode = knot.FindPropertyRelative(nameof(ArrowGraphic.LineKnot.Mode));
            if ((BezierTangentMode)mode.intValue == BezierTangentMode.Mirrored && IsInteriorKnot(knots, index))
            {
                // Splitting shortens the handle on this side. Preserve the two curves and keep them aligned.
                mode.intValue = (int)BezierTangentMode.Continuous;
            }
        }

        private static bool IsInteriorKnot(SerializedProperty knots, int index)
        {
            return index > 0 && index < knots.arraySize - 1;
        }

        private static Vector2 GetAuthoredTangent(SerializedProperty knots, int index, bool incoming)
        {
            var segment = incoming ? index - 1 : index;
            var first = ReadKnot(knots.GetArrayElementAtIndex(segment));
            var last = ReadKnot(knots.GetArrayElementAtIndex(segment + 1));
            var curve = ArrowGraphic.GetAuthoredCurve(in first, in last);
            return incoming ? curve.Incoming : curve.Outgoing;
        }

        private static ArrowGraphic.LineKnot ReadKnot(SerializedProperty knot)
        {
            return new ArrowGraphic.LineKnot
            {
                Position = knot.FindPropertyRelative("Position").vector2Value,
                PrevTangentPoint = ReadTangent(GetTangentProperty(knot, true)),
                NextTangentPoint = ReadTangent(GetTangentProperty(knot, false)),
            };
        }

        private static Optional<Vector2> ReadTangent(SerializedProperty tangent)
        {
            return tangent.FindPropertyRelative(TangentPresenceField).boolValue
                ? new Optional<Vector2>(tangent.FindPropertyRelative(TangentPositionField).vector2Value)
                : default;
        }

        private static SerializedProperty GetTangentProperty(SerializedProperty knot, bool incoming)
        {
            return knot.FindPropertyRelative(incoming ? "PrevTangentPoint" : "NextTangentPoint");
        }

        private static void WriteTangent(SerializedProperty tangent, Vector2 position)
        {
            tangent.FindPropertyRelative(TangentPresenceField).boolValue = true;
            tangent.FindPropertyRelative(TangentPositionField).vector2Value = position;
        }

        private static void WriteKnot(SerializedProperty knot, Vector2 position)
        {
            knot.FindPropertyRelative("Position").vector2Value = position;
            GetTangentProperty(knot, true).FindPropertyRelative(TangentPresenceField).boolValue = false;
            GetTangentProperty(knot, false).FindPropertyRelative(TangentPresenceField).boolValue = false;
            knot.FindPropertyRelative(nameof(ArrowGraphic.LineKnot.Mode)).intValue = (int)BezierTangentMode.Broken;
        }

        private static void TranslateManualTangents(SerializedProperty knot, Vector2 movement)
        {
            TranslateTangent(GetTangentProperty(knot, true), movement);
            TranslateTangent(GetTangentProperty(knot, false), movement);
        }

        private static void TranslateTangent(SerializedProperty tangent, Vector2 movement)
        {
            if (tangent.FindPropertyRelative(TangentPresenceField).boolValue)
            {
                var value = tangent.FindPropertyRelative(TangentPositionField);
                value.vector2Value += movement;
            }
        }

        private static void EnsurePathReference(ArrowGraphic arrow, SerializedProperty knots)
        {
            var reference = knots.serializedObject.FindProperty("_lastUsedRect");
            var rect = arrow.GetPathRect();
            if (!ArrowGraphic.IsUsableRect(reference.rectValue) && ArrowGraphic.IsUsableRect(rect))
            {
                reference.rectValue = rect;
            }
        }

        private void OnUndoRedo()
        {
            _pathEdit.OnUndoRedo();
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
