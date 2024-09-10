using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(ArrowGraphic)), CanEditMultipleObjects]
    public class ArrowGraphicEditor : Editor
    {
        private const float DefaultTangentDistance = 10.0f;
        
        private SerializedProperty _debugInfoProp;
        
        private void OnEnable()
        {
            _debugInfoProp = serializedObject.FindProperty("_meshInfo");
        }
        
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (target is not ArrowGraphic arrowGraphic)
            {
                return;
            }

            ref var debugInfo = ref arrowGraphic.MeshInfoAccess;
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField($"Verts: {debugInfo.Vertices}, Tris: {debugInfo.Triangles}");
            }
        }
        
        private void OnSceneGUI()
        {
            if (target is not ArrowGraphic arrow)
            {
                return;
            }

            var localTransform = arrow.rectTransform; 
            ref var settings = ref arrow.SettingsAccess;
            
            EditorGUI.BeginChangeCheck();

            if (settings.KnotsConfig.UseSpline)
            {
                DrawHandlesForBezier(ref settings, localTransform);
            }
            else
            {
                DrawHandlesForLerp(ref settings, localTransform);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Undo arrow changes");
                
                arrow.SetVerticesDirty();
                arrow.Rebuild(CanvasUpdate.LatePreRender);
            }
        }

        private void DrawHandlesForBezier(ref ArrowGraphic.Settings settings, RectTransform localTransform)
        {
            var knotsConfig = settings.KnotsConfig;
            var allKnots = knotsConfig.Knots;

            using var drawingScope = new Handles.DrawingScope(Color.yellow);

            for (var i = 0; i < allKnots.Length; i++)
            {
                ref var knot = ref allKnots[i];

                Handles.color = ObtainColor(i, allKnots.Length);

                var isStart = i == 0; 
                var isEnd = i == allKnots.Length - 1;

                var positionWorldSpace = localTransform.TransformPoint(knot.Position);

                var nextPosition = isEnd ? Vector2.zero : allKnots[i + 1].Position;
                var prevPosition = isStart ? Vector2.zero : allKnots[i - 1].Position;
                
                if (isStart)
                {
                    knot.NextTangentPoint = HandleTangentPoint(localTransform, knot.NextTangentPoint, knot.Position, in nextPosition, positionWorldSpace);
                }
                else if (isEnd)
                {
                    knot.PrevTangentPoint = HandleTangentPoint(localTransform, knot.PrevTangentPoint, knot.Position, in prevPosition, positionWorldSpace);
                }
                else
                {
                    knot.NextTangentPoint = HandleTangentPoint(localTransform, knot.NextTangentPoint, knot.Position, in nextPosition, positionWorldSpace);
                    knot.PrevTangentPoint = HandleTangentPoint(localTransform, knot.PrevTangentPoint, knot.Position, in prevPosition, positionWorldSpace);
                }
                
                Handles.DrawSolidDisc(positionWorldSpace, Vector3.back, 5);

                positionWorldSpace = Handles.PositionHandle(positionWorldSpace, Quaternion.identity);
                Vector2 newPosition = localTransform.InverseTransformPoint(positionWorldSpace);
                var diff = newPosition - knot.Position;

                if (knot.NextTangentPoint.HasValue)
                {
                    knot.NextTangentPoint.Value += diff;
                }
                
                if (knot.PrevTangentPoint.HasValue)
                {
                    knot.PrevTangentPoint.Value += diff;
                }

                knot.Position = newPosition;
            }

            settings.KnotsConfig = knotsConfig;
        }

        private Vector2? HandleTangentPoint(RectTransform localTransform, in Vector2? tangentPointToHandle, in Vector2 position, in Vector2 directionPosition, in Vector3 positionWorldSpace)
        {
            (bool isDefault, Vector2 tangentPoint) = CurrentOrDefault(in tangentPointToHandle, in position, in directionPosition);
            var tangentPointWorldSpace = localTransform.TransformPoint(tangentPoint);

            Handles.DrawLine(positionWorldSpace, tangentPointWorldSpace);
            Handles.DrawSolidDisc(tangentPointWorldSpace, Vector3.back, 2);

            var newTangentPointWorldSpace = Handles.PositionHandle(tangentPointWorldSpace, Quaternion.identity);
            if (isDefault && tangentPointWorldSpace == newTangentPointWorldSpace)
            {
                return null;
            }
            
            tangentPoint = localTransform.InverseTransformPoint(newTangentPointWorldSpace);
            return tangentPoint;
        }

        private (bool isDefault, Vector2 position) CurrentOrDefault(in Vector2? tangentPoint, in Vector2 position, in Vector2 directionPoint)
        {
            if (tangentPoint.HasValue)
            {
                return (false, tangentPoint.Value);
            }

            var diff = directionPoint - position;
            var direction = diff.normalized;

            return (true, position + (direction * DefaultTangentDistance));
        }

        private void DrawHandlesForLerp(ref ArrowGraphic.Settings settings, RectTransform localTransform)
        {
            var knotsConfig = settings.KnotsConfig;
            var allKnots = knotsConfig.Knots;

            using var drawingScope = new Handles.DrawingScope(Color.yellow);

            for (var i = 0; i < allKnots.Length; i++)
            {
                ref var knot = ref allKnots[i];

                Handles.color = ObtainColor(i, allKnots.Length);

                var positionWorldSpace = localTransform.TransformPoint(knot.Position);
                Handles.DrawSolidDisc(positionWorldSpace, Vector3.back, 5);

                positionWorldSpace = Handles.PositionHandle(positionWorldSpace, Quaternion.identity);
                knot.Position = localTransform.InverseTransformPoint(positionWorldSpace);
            }

            settings.KnotsConfig = knotsConfig;
        }

        private Color ObtainColor(int i, int totalLength)
        {
            if (i == 0)
            {
                return Color.yellow;
            }

            if (i == totalLength - 1)
            {
                return Color.cyan;
            }
            
            return Color.grey;
        }
    }
}
