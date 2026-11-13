#if UNITY_EDITOR && !ODIN_INSPECTOR
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
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

            var input = new VisualElement();
            input.style.flexDirection = FlexDirection.Row;
            input.style.alignItems = Align.Center;
            input.style.flexGrow = 1;
            input.style.minWidth = 0;
            var root = new MinMaxField(property.displayName, input) { tooltip = property.tooltip };

            // -------- NO RANGE: inline "Min" [field]  "Max" [field] --------
            if (range == null)
            {
                var minBox = MakeInlineLabelAndField("Min", out FloatField minField);
                minBox.style.flexGrow = 1;
                minBox.style.flexBasis = 0;
                minBox.style.minWidth = 0;
                minBox.style.marginRight = 6;

                var maxBox = MakeInlineLabelAndField("Max", out FloatField maxField);
                maxBox.style.flexGrow = 1;
                maxBox.style.flexBasis = 0;
                maxBox.style.minWidth = 0;

                minField.BindProperty(minProp);
                maxField.BindProperty(maxProp);

                input.Add(minBox);
                input.Add(maxBox);
                return root;
            }

            // -------- WITH RANGE: keep your current slider layout --------
            float limitMin = range.MinLimit;
            float limitMax = range.MaxLimit;

            var minInput = new FloatField();
            minInput.style.width = 90;
            minInput.style.minWidth = 0;
            minInput.style.flexShrink = 1;
            minInput.style.marginLeft = 0;
            minInput.style.marginRight = 6;
            minInput.BindProperty(minProp);
            input.Add(minInput);

            var slider = new MinMaxSlider(string.Empty, minProp.floatValue, maxProp.floatValue, limitMin, limitMax);
            slider.style.flexGrow = 1;
            slider.style.minWidth = 0;
            slider.style.flexShrink = 1;
            slider.style.marginLeft = 0;
            slider.style.marginRight = 6;
            input.Add(slider);

            var maxInput = new FloatField();
            maxInput.style.width = 90;
            maxInput.style.minWidth = 0;
            maxInput.style.flexShrink = 1;
            maxInput.style.marginLeft = 0;
            maxInput.style.marginRight = 0;
            maxInput.BindProperty(maxProp);
            input.Add(maxInput);

            void Apply(float min, float max)
            {
                minProp.floatValue = min;
                maxProp.floatValue = max;
                property.serializedObject.ApplyModifiedProperties();

                minInput.SetValueWithoutNotify(min);
                maxInput.SetValueWithoutNotify(max);
                slider.SetValueWithoutNotify(new Vector2(min, max));
            }

            void Refresh()
            {
                property.serializedObject.UpdateIfRequiredOrScript();
                minInput.SetValueWithoutNotify(minProp.floatValue);
                maxInput.SetValueWithoutNotify(maxProp.floatValue);
                slider.SetValueWithoutNotify(new Vector2(minProp.floatValue, maxProp.floatValue));
            }

            root.TrackPropertyValue(minProp, _ => Refresh());
            root.TrackPropertyValue(maxProp, _ => Refresh());
            root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);

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
            lbl.AddToClassList(BaseField<float>.labelDraggerVariantUssClassName);
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            lbl.style.marginRight = 4;
            // Similar vibe to Vector2 labels; tweak if you want tighter
            lbl.style.minWidth = 28;

            field = new FloatField();
            field.labelElement.style.display = DisplayStyle.None; // ensure no internal label space
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.minWidth = 0;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            var dragger = new FieldMouseDragger<float>(field);
            dragger.SetDragZone(lbl);

            box.Add(lbl);
            box.Add(field);
            return box;
        }

        private sealed class MinMaxField : BaseField<Vector2>
        {
            internal MinMaxField(string label, VisualElement input) : base(label, input)
            {
                AddToClassList(alignedFieldUssClassName);
            }
        }
    }
}
#endif
