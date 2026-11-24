using System;
using UnityEditor;
using UnityEngine;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(SplineTestGraphic)), CanEditMultipleObjects]
    public class SplineTestGraphicEditor : Editor
    {
        private static readonly GUIContent InsertPointLabel = new("Insert Point", "Insert a point between the selected point and the next point on the loop.");

        private readonly PathEditSession _pathEdit = new();
        private Vector3[] _previewPoints = Array.Empty<Vector3>();
        private int _selectedKnot;
        private bool _showDiagnostics;

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
            PrimitiveInspector.Section("Shape");
            var mode = PrimitiveInspector.DrawMode(serializedObject);
            PrimitiveInspector.Count(serializedObject.FindProperty("_samplesPerSegment"), "Samples per Span", 2, 48);
            if (targets.Length == 1 && target is SplineTestGraphic spline)
            {
                DrawPathInspector(spline, serializedObject.FindProperty("_knots"));
            }
            else
            {
                _pathEdit.SetTarget(null);
                EditorGUILayout.HelpBox("Select one spline to edit its path points.", MessageType.Info);
            }

            ClosedShapeInspector.DrawAppearance(mode, "_antiAliasingLayerWidth");

            var warning = target is SplineTestGraphic shape && shape.InvalidContour
                ? "The contour needs at least three distinct knots and no intersections."
                : null;
            PrimitiveInspector.DrawDiagnostics(serializedObject, ref _showDiagnostics, warning);
            if (serializedObject.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }
        }

        private void DrawPathInspector(SplineTestGraphic shape, SerializedProperty knots)
        {
            _selectedKnot = Mathf.Clamp(_selectedKnot, 0, Mathf.Max(0, knots.arraySize - 1));
            var wasEditing = _pathEdit.IsEditing(shape);
            var editPath = BezierPathInspector.DrawEditPathToggle(wasEditing);
            if (wasEditing != editPath)
            {
                _pathEdit.SetTarget(editPath ? shape : null);
                SceneView.RepaintAll();
            }

            var action = BezierPathInspector.DrawPointButtons(knots.arraySize, SplineTestGraphic.MinimumKnots,
                SplineTestGraphic.MaximumKnots, InsertPointLabel);
            if (action == BezierPathInspector.PointAction.Insert)
            {
                InsertPoint(knots);
            }
            else if (action == BezierPathInspector.PointAction.Remove)
            {
                knots.DeleteArrayElementAtIndex(_selectedKnot);
                _selectedKnot %= knots.arraySize;
            }

            if (knots.arraySize == 0)
            {
                return;
            }

            _selectedKnot = BezierPathInspector.DrawPointSelection(_selectedKnot, knots.arraySize);
            var point = knots.GetArrayElementAtIndex(_selectedKnot);
            EditorGUILayout.PropertyField(point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)),
                new GUIContent("Position", "Normalized coordinates in the RectTransform."));

            if (knots.arraySize < SplineTestGraphic.MinimumKnots)
            {
                return;
            }

            var mode = point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Mode));
            if (BezierPathInspector.DrawTangentMode(mode))
            {
                ApplyTangentMode(knots, point, (BezierTangentMode)mode.intValue);
            }

            var linked = (BezierTangentMode)mode.intValue != BezierTangentMode.Broken;
            DrawInspectorTangent(knots, point, true, linked);
            DrawInspectorTangent(knots, point, false, linked);
        }

        private void DrawInspectorTangent(SerializedProperty knots, SerializedProperty point, bool incoming, bool linked)
        {
            var automatic = GetAutomaticProperty(point, incoming);
            var offset = GetOffsetProperty(point, incoming);
            var label = incoming ? "Incoming Tangent" : "Outgoing Tangent";
            if (!linked)
            {
                var row = EditorGUILayout.GetControlRect();
                var caption = EditorGUI.BeginProperty(row, new GUIContent($"Set {label}"), automatic);
                var wasManual = !automatic.boolValue;
                var manual = EditorGUI.Toggle(row, caption, wasManual);
                EditorGUI.EndProperty();
                if (manual && !wasManual)
                {
                    WriteTangent(point, incoming, GetAuthoredTangent(knots, _selectedKnot, incoming));
                }
                else if (wasManual && !manual)
                {
                    automatic.boolValue = true;
                }
            }

            if (!automatic.boolValue || linked)
            {
                using var change = new EditorGUI.ChangeCheckScope();
                EditorGUILayout.PropertyField(offset, new GUIContent(label, "Offset from the point in normalized RectTransform coordinates."));
                if (change.changed)
                {
                    var position = point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value;
                    SetAuthoredTangent(knots, _selectedKnot, incoming, position + offset.vector2Value);
                }
            }
        }

        private void InsertPoint(SerializedProperty knots)
        {
            var count = knots.arraySize;
            if (count < SplineTestGraphic.MinimumKnots)
            {
                knots.arraySize = SplineTestGraphic.MinimumKnots;
                WriteAutomaticPoint(knots.GetArrayElementAtIndex(0), new Vector2(.15f, .25f));
                WriteAutomaticPoint(knots.GetArrayElementAtIndex(1), new Vector2(.55f, .85f));
                WriteAutomaticPoint(knots.GetArrayElementAtIndex(2), new Vector2(.85f, .25f));
                _selectedKnot = 0;
                return;
            }

            var firstIndex = _selectedKnot;
            var nextIndex = (firstIndex + 1) % count;
            var first = ReadKnot(knots.GetArrayElementAtIndex(firstIndex));
            var next = ReadKnot(knots.GetArrayElementAtIndex(nextIndex));
            var curve = SplineTestGraphic.GetCurve(in first, in next);
            curve.Split(0.5f, out var before, out var after);

            SetSplitTangent(knots.GetArrayElementAtIndex(firstIndex), false, before.Outgoing);
            SetSplitTangent(knots.GetArrayElementAtIndex(nextIndex), true, after.Incoming);

            int insertIndex = firstIndex + 1;
            if (insertIndex == count)
            {
                knots.arraySize = count + 1;
            }
            else
            {
                knots.InsertArrayElementAtIndex(insertIndex);
            }

            var inserted = knots.GetArrayElementAtIndex(insertIndex);
            WritePosition(inserted, before.End);
            WriteTangent(inserted, true, before.Incoming);
            WriteTangent(inserted, false, after.Outgoing);
            _selectedKnot = insertIndex;
        }

        private void OnSceneGUI()
        {
            if (target is not SplineTestGraphic spline || !_pathEdit.IsEditing(spline))
            {
                return;
            }

            var scenePath = _pathEdit.ScenePath;
            scenePath.UpdateIfRequiredOrScript();
            var knots = scenePath.FindProperty("_knots");
            var knotCount = knots.arraySize;
            var reference = spline.rectTransform;
            var rect = spline.GetPixelAdjustedRect();
            if (knotCount < SplineTestGraphic.MinimumKnots || knotCount > SplineTestGraphic.MaximumKnots ||
                rect.width <= 0f || rect.height <= 0f || !PathHandlePlane.TryCreate(reference, out var plane))
            {
                _pathEdit.EndDrag();
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                var detail = SplineTestGraphic.GetSegmentDetail(scenePath.FindProperty("_samplesPerSegment").intValue, knotCount);
                DrawPathPreview(reference, rect, knots, detail);
            }

            _selectedKnot = Mathf.Clamp(_selectedKnot, 0, knotCount - 1);
            var tangentKnot = _selectedKnot;
            for (var i = 0; i < knotCount; i++)
            {
                var knot = knots.GetArrayElementAtIndex(i);
                var position = knot.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position));
                var local = ToLocal(rect, position.vector2Value);
                if (!IsFinite(local))
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
                    _pathEdit.BeginDrag("Edit Spline Path");
                    position.vector2Value = ToNormalized(rect, reference.InverseTransformPoint(next));
                }
            }

            DrawSceneTangent(reference, rect, knots, tangentKnot, true, in plane);
            DrawSceneTangent(reference, rect, knots, tangentKnot, false, in plane);
            if (scenePath.ApplyModifiedProperties())
            {
                spline.SetVerticesDirty();
                Repaint();
                SceneView.RepaintAll();
            }

            if (GUIUtility.hotControl == 0)
            {
                _pathEdit.EndDrag();
            }
        }

        private void DrawPathPreview(RectTransform reference, Rect rect, SerializedProperty knots, int detail)
        {
            var count = knots.arraySize;
            var size = count * detail + 1;
            if (_previewPoints.Length != size)
            {
                _previewPoints = new Vector3[size];
            }

            var first = ReadKnot(knots.GetArrayElementAtIndex(0));
            var position = 0;
            for (var i = 0; i < count; i++)
            {
                var next = ReadKnot(knots.GetArrayElementAtIndex((i + 1) % count));
                var curve = SplineTestGraphic.GetCurve(in first, in next);
                for (var step = 0; step < detail; step++)
                {
                    var local = ToLocal(rect, curve.Evaluate((float)step / detail));
                    if (!IsFinite(local))
                    {
                        return;
                    }

                    _previewPoints[position++] = reference.TransformPoint(local);
                }

                first = next;
            }

            _previewPoints[^1] = _previewPoints[0];
            using var drawing = new Handles.DrawingScope(PathHandlePlane.PathColor);
            Handles.DrawAAPolyLine(2f, _previewPoints);
        }

        private void DrawSceneTangent(RectTransform reference, Rect rect, SerializedProperty knots, int index,
            bool incoming, in PathHandlePlane plane)
        {
            var point = knots.GetArrayElementAtIndex(index);
            var localAnchor = ToLocal(rect, point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value);
            var localTangent = ToLocal(rect, GetAuthoredTangent(knots, index, incoming));
            if (!IsFinite(localAnchor) || !IsFinite(localTangent))
            {
                return;
            }

            var anchor = reference.TransformPoint(localAnchor);
            var world = reference.TransformPoint(localTangent);
            var next = plane.DrawTangent(anchor, world);
            if (next != world)
            {
                _pathEdit.BeginDrag("Edit Spline Path");
                SetAuthoredTangent(knots, index, incoming, ToNormalized(rect, reference.InverseTransformPoint(next)));
            }
        }

        private void ApplyTangentMode(SerializedProperty knots, SerializedProperty point, BezierTangentMode mode)
        {
            if (mode == BezierTangentMode.Broken)
            {
                return;
            }

            var knot = ReadKnot(point);
            var incoming = GetAuthoredTangent(knots, _selectedKnot, true);
            var outgoing = GetAuthoredTangent(knots, _selectedKnot, false);
            var anchorIncoming = (!knot.AutoIncoming && knot.AutoOutgoing) ||
                (outgoing == knot.Position && incoming != knot.Position);
            SetAuthoredTangent(knots, _selectedKnot, anchorIncoming, anchorIncoming ? incoming : outgoing);
        }

        private static void SetAuthoredTangent(SerializedProperty knots, int index, bool incoming, Vector2 tangent)
        {
            var point = knots.GetArrayElementAtIndex(index);
            WriteTangent(point, incoming, tangent);
            var mode = (BezierTangentMode)point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Mode)).intValue;
            if (mode != BezierTangentMode.Broken)
            {
                var anchor = point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value;
                var opposite = GetAuthoredTangent(knots, index, !incoming);
                WriteTangent(point, !incoming, BezierPath.GetOppositeTangent(mode, anchor, tangent, opposite));
            }
        }

        private static void SetSplitTangent(SerializedProperty point, bool incoming, Vector2 tangent)
        {
            WriteTangent(point, incoming, tangent);
            var mode = point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Mode));
            if ((BezierTangentMode)mode.intValue == BezierTangentMode.Mirrored)
            {
                // Splitting shortens one handle. Continuous keeps the existing shape and alignment.
                mode.intValue = (int)BezierTangentMode.Continuous;
            }
        }

        private static Vector2 GetAuthoredTangent(SerializedProperty knots, int index, bool incoming)
        {
            var count = knots.arraySize;
            var firstIndex = incoming ? (index + count - 1) % count : index;
            var first = ReadKnot(knots.GetArrayElementAtIndex(firstIndex));
            var last = ReadKnot(knots.GetArrayElementAtIndex((firstIndex + 1) % count));
            var curve = SplineTestGraphic.GetCurve(in first, in last);
            return incoming ? curve.Incoming : curve.Outgoing;
        }

        private static SplineTestGraphic.Knot ReadKnot(SerializedProperty point)
        {
            return new SplineTestGraphic.Knot(
                point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value,
                GetOffsetProperty(point, true).vector2Value, GetOffsetProperty(point, false).vector2Value)
            {
                AutoIncoming = GetAutomaticProperty(point, true).boolValue,
                AutoOutgoing = GetAutomaticProperty(point, false).boolValue,
            };
        }

        private static void WritePosition(SerializedProperty point, Vector2 position)
        {
            point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value = position;
            point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Mode)).intValue = (int)BezierTangentMode.Broken;
        }

        private static void WriteAutomaticPoint(SerializedProperty point, Vector2 position)
        {
            WritePosition(point, position);
            GetAutomaticProperty(point, true).boolValue = true;
            GetAutomaticProperty(point, false).boolValue = true;
        }

        private static void WriteTangent(SerializedProperty point, bool incoming, Vector2 tangent)
        {
            var position = point.FindPropertyRelative(nameof(SplineTestGraphic.Knot.Position)).vector2Value;
            GetOffsetProperty(point, incoming).vector2Value = tangent - position;
            GetAutomaticProperty(point, incoming).boolValue = false;
        }

        private static SerializedProperty GetOffsetProperty(SerializedProperty point, bool incoming)
        {
            return point.FindPropertyRelative(incoming ? nameof(SplineTestGraphic.Knot.Incoming) : nameof(SplineTestGraphic.Knot.Outgoing));
        }

        private static SerializedProperty GetAutomaticProperty(SerializedProperty point, bool incoming)
        {
            return point.FindPropertyRelative(incoming ? nameof(SplineTestGraphic.Knot.AutoIncoming) : nameof(SplineTestGraphic.Knot.AutoOutgoing));
        }

        private static Vector2 ToLocal(Rect rect, Vector2 normalized)
        {
            return rect.min + Vector2.Scale(rect.size, normalized);
        }

        private static Vector2 ToNormalized(Rect rect, Vector2 local)
        {
            return new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        }

        private static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        private void OnUndoRedo()
        {
            _pathEdit.OnUndoRedo();
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
