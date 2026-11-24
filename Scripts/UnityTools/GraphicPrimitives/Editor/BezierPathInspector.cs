using UnityEditor;
using UnityEngine;

namespace Tools.EditorOnly.GraphicPrimitives
{
    internal static class BezierPathInspector
    {
        private static readonly GUIContent EditPathLabel = new("Edit Path");
        private static readonly GUIContent RemovePointLabel = new("Remove Point", "Remove the selected point from the path.");
        private static readonly GUIContent TangentModeLabel = new("Tangent Mode",
            "Broken keeps handles independent. Mirrored keeps handles opposite and equal in length. Continuous keeps handles opposite with separate lengths.");

        internal enum PointAction
        {
            None,
            Insert,
            Remove,
        }

        internal static bool DrawEditPathToggle(bool editing)
        {
            return GUILayout.Toggle(editing, EditPathLabel, GUI.skin.button);
        }

        internal static PointAction DrawPointButtons(int count, int minimum, int maximum, GUIContent insertPointLabel)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool insert;
                using (new EditorGUI.DisabledScope(count >= maximum))
                {
                    insert = GUILayout.Button(insertPointLabel);
                }

                bool remove;
                using (new EditorGUI.DisabledScope(count <= minimum))
                {
                    remove = GUILayout.Button(RemovePointLabel);
                }

                if (insert)
                {
                    return PointAction.Insert;
                }

                if (remove)
                {
                    return PointAction.Remove;
                }
            }

            return PointAction.None;
        }

        internal static int DrawPointSelection(int index, int count)
        {
            var selected = EditorGUILayout.IntSlider("Selected Point", index + 1, 1, count) - 1;
            if (index != selected)
            {
                SceneView.RepaintAll();
            }

            return selected;
        }

        internal static bool DrawTangentMode(SerializedProperty mode, bool enabled = true)
        {
            using var change = new EditorGUI.ChangeCheckScope();
            using var disabled = new EditorGUI.DisabledScope(!enabled);
            EditorGUILayout.PropertyField(mode, TangentModeLabel);
            return enabled && change.changed;
        }
    }

    internal readonly struct PathHandlePlane
    {
        internal static readonly Color PathColor = new(0.45f, 0.85f, 1f);
        private static readonly Color SelectedPointColor = new(1f, 0.77f, 0.28f);
        private static readonly Color TangentColor = new(0.55f, 0.95f, 0.72f);
        private const float PointHandleSize = 0.08f;
        private const float TangentHandleSize = 0.055f;

        private readonly Vector3 _right;
        private readonly Vector3 _up;
        private readonly Vector3 _normal;

        private PathHandlePlane(Vector3 right, Vector3 up, Vector3 normal)
        {
            _right = right;
            _up = up;
            _normal = normal;
        }

        internal static bool TryCreate(RectTransform reference, out PathHandlePlane plane)
        {
            var right = reference.TransformVector(Vector3.right).normalized;
            var normal = Vector3.Cross(right, reference.TransformVector(Vector3.up).normalized).normalized;
            if (normal == Vector3.zero)
            {
                plane = default;
                return false;
            }

            plane = new PathHandlePlane(right, Vector3.Cross(normal, right), normal);
            return true;
        }

        internal Vector3 DrawPoint(Vector3 world, bool selected, out bool selectPoint)
        {
            var size = HandleUtility.GetHandleSize(world) * PointHandleSize;
            var hotControl = GUIUtility.hotControl;
            using var drawing = new Handles.DrawingScope(selected ? SelectedPointColor : PathColor);
            var next = Handles.Slider2D(world, _normal, _right, _up, size, Handles.DotHandleCap, Vector2.zero);
            selectPoint = (hotControl == 0 && GUIUtility.hotControl != 0) || next != world;
            return next;
        }

        internal Vector3 DrawTangent(Vector3 anchor, Vector3 world)
        {
            var size = HandleUtility.GetHandleSize(world) * TangentHandleSize;
            using var drawing = new Handles.DrawingScope(TangentColor);
            Handles.DrawLine(anchor, world);
            return Handles.Slider2D(world, _normal, _right, _up, size, Handles.RectangleHandleCap, Vector2.zero);
        }
    }

    internal sealed class PathEditSession
    {
        private Object _target;
        private int _dragUndoGroup = -1;

        internal SerializedObject ScenePath { get; private set; }

        internal bool IsEditing(Object target)
        {
            return _target == target && ScenePath != null;
        }

        internal void SetTarget(Object target)
        {
            if (ReferenceEquals(_target, target))
            {
                return;
            }

            EndDrag();
            ScenePath?.Dispose();
            _target = target;

            // OnSceneGUI is invoked for each target individually. Its serialized edit must be too.
            ScenePath = target != null ? new SerializedObject(target) : null;
        }

        internal void BeginDrag(string label)
        {
            if (_dragUndoGroup >= 0)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            _dragUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
        }

        internal void EndDrag()
        {
            if (_dragUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_dragUndoGroup);
                _dragUndoGroup = -1;
            }
        }

        internal void OnUndoRedo()
        {
            _dragUndoGroup = -1;
        }
    }
}
