#if UNITY_EDITOR && ODIN_INSPECTOR
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityTools;

namespace Tools.Editor
{
    public sealed class MinMaxIntOdinDrawer : OdinValueDrawer<MinMaxInt>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var range = this.Property.GetAttribute<MinMaxRangeAttribute>();

            MinMaxInt value = this.ValueEntry.SmartValue;
            int minI = value.Min;
            int maxI = value.Max;

            SirenixEditorGUI.BeginHorizontalPropertyLayout(label);

            if (range == null)
            {
                float old = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 28f;

                minI = EditorGUILayout.IntField("Min", minI);
                GUILayout.Space(6);
                maxI = EditorGUILayout.IntField("Max", maxI);

                EditorGUIUtility.labelWidth = old;
            }
            else
            {
                // With range: [min] slider [max]
                minI = EditorGUILayout.IntField(minI, GUILayout.Width(90));

                int limitMin = Mathf.RoundToInt(range.MinLimit);
                int limitMax = Mathf.RoundToInt(range.MaxLimit);

                // MinMaxSlider is float-based; round back to int
                float minF = minI;
                float maxF = maxI;
                EditorGUILayout.MinMaxSlider(ref minF, ref maxF, limitMin, limitMax);
                minI = Mathf.RoundToInt(minF);
                maxI = Mathf.RoundToInt(maxF);

                maxI = EditorGUILayout.IntField(maxI, GUILayout.Width(90));
            }

            SirenixEditorGUI.EndHorizontalPropertyLayout();

            this.ValueEntry.SmartValue = new MinMaxInt(minI, maxI);
        }
    }
}
#endif