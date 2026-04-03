#if UNITY_EDITOR && ODIN_INSPECTOR
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityTools;

namespace Tools.Editor
{
    public sealed class MinMaxOdinDrawer : OdinValueDrawer<MinMax>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var range = this.Property.GetAttribute<MinMaxRangeAttribute>();

            MinMax value = this.ValueEntry.SmartValue;
            float min = value.Min;
            float max = value.Max;

            SirenixEditorGUI.BeginHorizontalPropertyLayout(label);

            if (range == null)
            {
                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 28f;

                min = EditorGUILayout.FloatField("Min", min);
                GUILayout.Space(6);
                max = EditorGUILayout.FloatField("Max", max);

                EditorGUIUtility.labelWidth = old;
            }
            else
            {
                // With range: [min] slider [max]
                min = EditorGUILayout.FloatField(min, GUILayout.Width(90));

                float limitMin = range.MinLimit;
                float limitMax = range.MaxLimit;

                EditorGUILayout.MinMaxSlider(ref min, ref max, limitMin, limitMax);

                max = EditorGUILayout.FloatField(max, GUILayout.Width(90));
            }

            SirenixEditorGUI.EndHorizontalPropertyLayout();

            // Write back (struct copy!)
            this.ValueEntry.SmartValue = new MinMax(min, max);
        }
    }
}
#endif