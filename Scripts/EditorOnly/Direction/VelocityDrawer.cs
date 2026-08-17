using UnityEngine;
using UnityEditor;
using UnityTools;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Tools.Editor
{
    [CustomPropertyDrawer(typeof(VelocityAttribute))]
    public sealed class VelocityPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.Vector2
                       ? DirectionDrawerHelper.TotalHeight
                       : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.HelpBox(position, "[Velocity] is only valid on Vector2 fields.", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            var result = VelocityDrawerHelper.Draw(position, label, property.vector2Value);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector2Value = result;
            }

            EditorGUI.EndProperty();
        }
    }

#if ODIN_INSPECTOR
    public sealed class VelocityOdinDrawer : OdinAttributeDrawer<VelocityAttribute, Vector2>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect(true, DirectionDrawerHelper.TotalHeight);
            var displayLabel = label ?? new GUIContent(Property.NiceName);
            var result = VelocityDrawerHelper.Draw(rect, displayLabel, ValueEntry.SmartValue);
            if (result != ValueEntry.SmartValue)
            {
                ValueEntry.SmartValue = result;
            }
        }
    }
#endif

    internal static class VelocityDrawerHelper
    {
        private const float CompassSize = 56f;

        public static Vector2 Draw(Rect position, GUIContent label, Vector2 v)
        {
            var lineH   = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var labelW  = EditorGUIUtility.labelWidth;
            var contentX = position.x + labelW;
            var prevLW  = EditorGUIUtility.labelWidth;
            var totalH  = DirectionDrawerHelper.TotalHeight;

            EditorGUI.LabelField(
                new Rect(position.x, position.y + (totalH - lineH) * 0.5f, labelW - 2f, lineH),
                label
            );

            float mag  = v.magnitude;
            var dir    = mag > 1e-6f ? v / mag : Vector2.right;
            var angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            // X/Y read-only
            const float XyColW = 68f;
            var xyY = position.y + (totalH - 2f * lineH - spacing) * 0.5f;
            EditorGUIUtility.labelWidth = 13f;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.FloatField(new Rect(contentX, xyY, XyColW, lineH), new GUIContent("X"), v.x);
                EditorGUI.FloatField(new Rect(contentX, xyY + lineH + spacing, XyColW, lineH), new GUIContent("Y"), v.y);
            }
            EditorGUIUtility.labelWidth = prevLW;

            // Compass — returns a unit dir; we reapply magnitude after.
            var compassX    = contentX + XyColW + 4f;
            var compassRect = new Rect(compassX, position.y + (totalH - CompassSize) * 0.5f, CompassSize, CompassSize);
            EditorGUI.BeginChangeCheck();
            var newDir = DirectionDrawerHelper.DrawCompass(compassRect, dir, angle, out var compassAngle);
            if (EditorGUI.EndChangeCheck())
            {
                dir   = newDir;
                angle = compassAngle;
            }

            // Right column: Angle (top) + Value (bottom)
            var rightX = compassX + CompassSize + 4f;
            var rightW = position.xMax - rightX - 2f;
            var rowY   = position.y + (totalH - 2f * lineH - spacing) * 0.5f;
            EditorGUIUtility.labelWidth = 38f;

            EditorGUI.BeginChangeCheck();
            var newAngle = EditorGUI.FloatField(
                new Rect(rightX, rowY, rightW, lineH),
                new GUIContent("Angle"), angle);
            if (EditorGUI.EndChangeCheck())
            {
                newAngle = (newAngle % 360f + 360f) % 360f;
                var rad = newAngle * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            EditorGUI.BeginChangeCheck();
            var newMag = EditorGUI.FloatField(
                new Rect(rightX, rowY + lineH + spacing, rightW, lineH),
                new GUIContent("Value"), mag);
            if (EditorGUI.EndChangeCheck())
            {
                mag = newMag;
            }

            EditorGUIUtility.labelWidth = prevLW;

            return dir * mag;
        }
    }
}
