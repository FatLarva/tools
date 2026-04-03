#if UNITY_EDITOR && !ODIN_INSPECTOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityTools;

namespace Tools.Editor
{
    [CustomPropertyDrawer(typeof(MinMaxInt))]
    public sealed class MinMaxIntDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var minProp = property.FindPropertyRelative("_min");
            var maxProp = property.FindPropertyRelative("_max");

            var range = fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(true);

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;

            var label = new Label(property.displayName) { tooltip = property.tooltip };
            label.style.minWidth = EditorGUIUtility.labelWidth;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginRight = 6;
            root.Add(label);

            // -------- NO RANGE: inline "Min" [field]  "Max" [field] --------
            if (range == null)
            {
                var minBox = MakeInlineLabelAndField("Min", out var minField);
                minBox.style.flexGrow = 1;
                minBox.style.marginRight = 6;

                var maxBox = MakeInlineLabelAndField("Max", out var maxField);
                maxBox.style.flexGrow = 1;

                minField.isDelayed = true;
                maxField.isDelayed = true;

                minField.SetValueWithoutNotify(minProp.intValue);
                maxField.SetValueWithoutNotify(maxProp.intValue);

                minField.RegisterValueChangedCallback(e =>
                {
                    minProp.intValue = e.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });

                maxField.RegisterValueChangedCallback(e =>
                {
                    maxProp.intValue = e.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });

                root.Add(minBox);
                root.Add(maxBox);
                return root;
            }

            // -------- WITH RANGE: keep your current slider layout --------
            var limitMin = Mathf.RoundToInt(range.MinLimit);
            var limitMax = Mathf.RoundToInt(range.MaxLimit);

            var minInput = new IntegerField { isDelayed = true };
            minInput.style.width = 90;
            minInput.style.marginRight = 6;
            minInput.SetValueWithoutNotify(minProp.intValue);
            root.Add(minInput);

            var slider = new MinMaxSlider(string.Empty, minProp.intValue, maxProp.intValue, limitMin, limitMax);
            slider.style.flexGrow = 1;
            slider.style.marginRight = 6;
            root.Add(slider);

            var maxInput = new IntegerField { isDelayed = true };
            maxInput.style.width = 90;
            maxInput.SetValueWithoutNotify(maxProp.intValue);
            root.Add(maxInput);

            void Apply(int min, int max)
            {
                minProp.intValue = min;
                maxProp.intValue = max;
                property.serializedObject.ApplyModifiedProperties();

                minInput.SetValueWithoutNotify(min);
                maxInput.SetValueWithoutNotify(max);
                slider.SetValueWithoutNotify(new Vector2(min, max));
            }

            Apply(minProp.intValue, maxProp.intValue);

            minInput.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(e.newValue, maxProp.intValue);
            });

            maxInput.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(minProp.intValue, e.newValue);
            });

            slider.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(Mathf.RoundToInt(e.newValue.x), Mathf.RoundToInt(e.newValue.y));
            });

            return root;
        }

        private static VisualElement MakeInlineLabelAndField(string text, out IntegerField field)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;

            var lbl = new Label(text);
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            lbl.style.marginRight = 4;
            lbl.style.minWidth = 28;

            field = new IntegerField();
            field.labelElement.style.display = DisplayStyle.None;
            field.style.flexGrow = 1;
            field.style.minWidth = 40;

            box.Add(lbl);
            box.Add(field);
            return box;
        }
    }
}

#endif