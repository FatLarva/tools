#if UNITY_EDITOR && !ODIN_INSPECTOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityTools;

namespace Tools.Editor
{
    [CustomPropertyDrawer(typeof(MinMax))]
    public sealed class MinMaxDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var minProp = property.FindPropertyRelative("_min");
            var maxProp = property.FindPropertyRelative("_max");

            var range = fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(inherit: true);

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
                var minBox = MakeInlineLabelAndField("Min", out FloatField minField);
                minBox.style.flexGrow = 1;
                minBox.style.marginRight = 6;

                var maxBox = MakeInlineLabelAndField("Max", out FloatField maxField);
                maxBox.style.flexGrow = 1;

                minField.isDelayed = true;
                maxField.isDelayed = true;

                minField.SetValueWithoutNotify(minProp.floatValue);
                maxField.SetValueWithoutNotify(maxProp.floatValue);

                minField.RegisterValueChangedCallback(e =>
                {
                    minProp.floatValue = e.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });

                maxField.RegisterValueChangedCallback(e =>
                {
                    maxProp.floatValue = e.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });

                root.Add(minBox);
                root.Add(maxBox);
                return root;
            }

            // -------- WITH RANGE: keep your current slider layout --------
            float limitMin = range.MinLimit;
            float limitMax = range.MaxLimit;

            var minInput = new FloatField { isDelayed = true };
            minInput.style.width = 90;
            minInput.style.marginRight = 6;
            minInput.SetValueWithoutNotify(minProp.floatValue);
            root.Add(minInput);

            var slider = new MinMaxSlider(string.Empty, minProp.floatValue, maxProp.floatValue, limitMin, limitMax);
            slider.style.flexGrow = 1;
            slider.style.marginRight = 6;
            root.Add(slider);

            var maxInput = new FloatField { isDelayed = true };
            maxInput.style.width = 90;
            maxInput.SetValueWithoutNotify(maxProp.floatValue);
            root.Add(maxInput);

            void Apply(float min, float max)
            {
                minProp.floatValue = min;
                maxProp.floatValue = max;
                property.serializedObject.ApplyModifiedProperties();

                minInput.SetValueWithoutNotify(min);
                maxInput.SetValueWithoutNotify(max);
                slider.SetValueWithoutNotify(new Vector2(min, max));
            }

            Apply(minProp.floatValue, maxProp.floatValue);

            minInput.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(e.newValue, maxProp.floatValue);
            });

            maxInput.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(minProp.floatValue, e.newValue);
            });

            slider.RegisterValueChangedCallback(e =>
            {
                property.serializedObject.Update();
                Apply(e.newValue.x, e.newValue.y);
            });

            return root;
        }

        private static VisualElement MakeInlineLabelAndField(string text, out FloatField field)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;

            var lbl = new Label(text);
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            lbl.style.marginRight = 4;
            // Similar vibe to Vector2 labels; tweak if you want tighter
            lbl.style.minWidth = 28;

            field = new FloatField();
            field.labelElement.style.display = DisplayStyle.None; // ensure no internal label space
            field.style.flexGrow = 1;
            field.style.minWidth = 40;

            box.Add(lbl);
            box.Add(field);
            return box;
        }
    }
}
#endif
