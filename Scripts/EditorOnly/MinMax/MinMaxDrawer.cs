using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityTools;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#else
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif

namespace Tools.Editor
{
#if !ODIN_INSPECTOR
    [CustomPropertyDrawer(typeof(MinMax))]
    public sealed class MinMaxDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return MinMaxDrawerControls.CreateInspector(property, fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(true), false);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            MinMaxDrawerControls.DrawProperty(position, property, label, fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(true), false);
        }
    }

    [CustomPropertyDrawer(typeof(MinMaxInt))]
    public sealed class MinMaxIntDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return MinMaxDrawerControls.CreateInspector(property, fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(true), true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            MinMaxDrawerControls.DrawProperty(position, property, label, fieldInfo?.GetCustomAttribute<MinMaxRangeAttribute>(true), true);
        }
    }
#else
    public sealed class MinMaxOdinDrawer : OdinValueDrawer<MinMax>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            MinMaxDrawerControls.DrawOdin(ValueEntry, label, Property.GetAttribute<MinMaxRangeAttribute>(), false,
                value => new MinMaxDrawerControls.Endpoints(value.Min, value.Max),
                value => new MinMax((float)value.Min, (float)value.Max));
        }
    }

    public sealed class MinMaxIntOdinDrawer : OdinValueDrawer<MinMaxInt>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            MinMaxDrawerControls.DrawOdin(ValueEntry, label, Property.GetAttribute<MinMaxRangeAttribute>(), true,
                value => new MinMaxDrawerControls.Endpoints(value.Min, value.Max),
                value => new MinMaxInt((int)value.Min, (int)value.Max));
        }
    }
#endif

    internal static class MinMaxDrawerControls
    {
        private const float Spacing = 6f;
        private const float RangeFieldWidth = 90f;
        private const double SliderRoundingMargin = 4d / (1 << 23);
        private static readonly GUIContent MinLabel = new("Min");
        private static readonly GUIContent MaxLabel = new("Max");

        internal readonly struct Endpoints
        {
            internal readonly double Min;
            internal readonly double Max;

            internal Endpoints(double min, double max)
            {
                Min = min;
                Max = max;
            }
        }

        private readonly struct Edit
        {
            internal readonly double? Min;
            internal readonly double? Max;

            internal Edit(double? min, double? max)
            {
                Min = min;
                Max = max;
            }

            internal bool HasChange
            {
                get { return Min.HasValue || Max.HasValue; }
            }
        }

        private static Endpoints GetLimits(MinMaxRangeAttribute range, bool integer)
        {
            var typeLimits = integer ? new Endpoints(int.MinValue, int.MaxValue) : new Endpoints(-float.MaxValue, float.MaxValue);
            if (range == null || !float.IsFinite(range.MinLimit) || !float.IsFinite(range.MaxLimit) || range.MinLimit > range.MaxLimit)
            {
                return typeLimits;
            }

            if (!integer)
            {
                return new Endpoints(range.MinLimit, range.MaxLimit);
            }

            return new Endpoints(
                Math.Clamp(Math.Round((double)range.MinLimit), typeLimits.Min, typeLimits.Max),
                Math.Clamp(Math.Round((double)range.MaxLimit), typeLimits.Min, typeLimits.Max));
        }

        private static Endpoints ApplyEdit(Endpoints current, Edit edit, Endpoints limits)
        {
            if (edit.Min.HasValue && double.IsFinite(edit.Min.Value) && edit.Max.HasValue && double.IsFinite(edit.Max.Value))
            {
                var min = Math.Clamp(edit.Min.Value, limits.Min, limits.Max);
                var max = Math.Clamp(edit.Max.Value, limits.Min, limits.Max);
                return new Endpoints(Math.Min(min, max), Math.Max(min, max));
            }

            if (edit.Min.HasValue && double.IsFinite(edit.Min.Value) && current.Max >= limits.Min)
            {
                return new Endpoints(Math.Clamp(edit.Min.Value, limits.Min, Math.Min(limits.Max, current.Max)), current.Max);
            }

            if (edit.Max.HasValue && double.IsFinite(edit.Max.Value) && current.Min <= limits.Max)
            {
                return new Endpoints(current.Min, Math.Clamp(edit.Max.Value, Math.Max(limits.Min, current.Min), limits.Max));
            }

            return current;
        }

        private static Edit SliderEdit(Vector2 previous, Vector2 next, bool integer)
        {
            return new Edit(previous.x == next.x ? null : SliderValue(next.x, integer),
                previous.y == next.y ? null : SliderValue(next.y, integer));
        }

        private static double SliderValue(float value, bool integer)
        {
            return integer ? Math.Round((double)value) : value;
        }

        private static Edit ImGuiSliderEdit(Vector2 previous, Vector2 next, bool integer)
        {
            if (previous.x != next.x && previous.y != next.y)
            {
                // IMGUI reconstructs max as min + range after a drag. Float
                // round-off there must not turn a minimum drag into a two-endpoint edit.
                var magnitude = Math.Max(Math.Max(Math.Abs((double)previous.x), Math.Abs((double)previous.y)), Math.Abs((double)next.x));
                var reconstructedMaxTolerance = magnitude * SliderRoundingMargin;
                if (Math.Abs((double)next.y - previous.y) <= reconstructedMaxTolerance)
                {
                    return new Edit(SliderValue(next.x, integer), null);
                }
            }

            return SliderEdit(previous, next, integer);
        }

        private static Edit DrawImGui(Rect position, GUIContent label, Endpoints current, MinMaxRangeAttribute range,
            Endpoints limits, bool integer, bool mixedMin, bool mixedMax)
        {
            var content = label == null ? EditorGUI.IndentedRect(position) : EditorGUI.PrefixLabel(position, label);
            using var indentation = new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel);
            if (!CanDrawSlider(range, limits))
            {
                var width = Mathf.Max(0f, (content.width - Spacing) * 0.5f);
                var minRect = new Rect(content.x, content.y, width, content.height);
                var maxRect = new Rect(content.xMax - width, content.y, width, content.height);
                var oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 28f;
                try
                {
                    return new Edit(DrawNumber(minRect, MinLabel, current.Min, integer, mixedMin),
                        DrawNumber(maxRect, MaxLabel, current.Max, integer, mixedMax));
                }
                finally
                {
                    EditorGUIUtility.labelWidth = oldLabelWidth;
                }
            }

            var fieldWidth = Mathf.Min(RangeFieldWidth, Mathf.Max(0f, (content.width - 2f * Spacing) / 3f));
            var minField = new Rect(content.x, content.y, fieldWidth, content.height);
            var maxField = new Rect(content.xMax - fieldWidth, content.y, fieldWidth, content.height);
            var slider = new Rect(minField.xMax + Spacing, content.y, Mathf.Max(0f, maxField.xMin - minField.xMax - 2f * Spacing), content.height);
            var minEdit = DrawNumber(minField, GUIContent.none, current.Min, integer, mixedMin);

            var sliderMin = (float)current.Min;
            var sliderMax = (float)current.Max;
            var previous = new Vector2(sliderMin, sliderMax);
            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(slider, ref sliderMin, ref sliderMax, (float)limits.Min, (float)limits.Max);
            var sliderEdit = EditorGUI.EndChangeCheck() ? ImGuiSliderEdit(previous, new Vector2(sliderMin, sliderMax), integer) : default;

            var maxEdit = DrawNumber(maxField, GUIContent.none, current.Max, integer, mixedMax);
            return new Edit(minEdit ?? sliderEdit.Min, maxEdit ?? sliderEdit.Max);
        }

        private static double? DrawNumber(Rect rect, GUIContent label, double current, bool integer, bool mixed)
        {
            using var scope = new EditorGUI.MixedValueScope(mixed);
            EditorGUI.BeginChangeCheck();
            double result = integer
                ? (double)EditorGUI.DelayedIntField(rect, label, (int)current)
                : EditorGUI.DelayedFloatField(rect, label, (float)current);
            return EditorGUI.EndChangeCheck() ? result : null;
        }

        private static bool CanDrawSlider(MinMaxRangeAttribute range, Endpoints limits)
        {
            return range != null && (float)limits.Min < (float)limits.Max && float.IsFinite((float)(limits.Max - limits.Min));
        }

#if ODIN_INSPECTOR
        internal static void DrawOdin<T>(IPropertyValueEntry<T> entry, GUIContent label, MinMaxRangeAttribute range,
            bool integer, Func<T, Endpoints> read, Func<Endpoints, T> create)
        {
            if (entry.ValueCount == 0)
            {
                return;
            }

            var values = entry.Values;
            var current = read(values[0]);
            var mixedMin = false;
            var mixedMax = false;
            for (int i = 1; i < entry.ValueCount; i++)
            {
                var other = read(values[i]);
                mixedMin |= !current.Min.Equals(other.Min);
                mixedMax |= !current.Max.Equals(other.Max);
            }

            var limits = GetLimits(range, integer);
            var edit = DrawImGui(EditorGUILayout.GetControlRect(), label, current, range, limits, integer, mixedMin, mixedMax);
            if (edit.HasChange)
            {
                for (int i = 0; i < entry.ValueCount; i++)
                {
                    var before = read(values[i]);
                    var after = ApplyEdit(before, edit, limits);
                    if (!before.Min.Equals(after.Min) || !before.Max.Equals(after.Max))
                    {
                        values[i] = create(after);
                    }
                }
            }
        }
#else
        internal static void DrawProperty(Rect position, SerializedProperty property, GUIContent label, MinMaxRangeAttribute range, bool integer)
        {
            using var scope = new EditorGUI.PropertyScope(position, label, property);
            var min = property.FindPropertyRelative("_min");
            var max = property.FindPropertyRelative("_max");
            var current = ReadEndpoints(min, max, integer);
            var limits = GetLimits(range, integer);
            var edit = DrawImGui(position, scope.content, current, range, limits, integer, min.hasMultipleDifferentValues, max.hasMultipleDifferentValues);
            if (edit.HasChange)
            {
                ApplySerializedEdit(property, edit, limits, integer, false);
            }
        }

        internal static VisualElement CreateInspector(SerializedProperty property, MinMaxRangeAttribute range, bool integer)
        {
            var min = property.FindPropertyRelative("_min");
            var max = property.FindPropertyRelative("_max");
            var limits = GetLimits(range, integer);
            var input = new VisualElement();
            input.style.flexDirection = FlexDirection.Row;
            input.style.alignItems = Align.Center;
            input.style.flexGrow = 1;
            input.style.minWidth = 0;
            var root = new RangeField(property.displayName, input) { tooltip = property.tooltip };
            var hasSlider = CanDrawSlider(range, limits);
            MinMaxSlider slider = null;

            var minInput = integer
                ? MakeNumberField(new IntegerField(), min, "Min", hasSlider, value => value, value => (int)value, CommitMin)
                : MakeNumberField(new FloatField(), min, "Min", hasSlider, value => value, value => (float)value, CommitMin);
            minInput.style.marginRight = Spacing;
            input.Add(minInput);

            if (hasSlider)
            {
                var current = ReadEndpoints(min, max, integer);
                slider = new MinMaxSlider(string.Empty, (float)current.Min, (float)current.Max, (float)limits.Min, (float)limits.Max);
                slider.style.flexGrow = 1;
                slider.style.flexShrink = 1;
                slider.style.minWidth = 0;
                slider.style.marginLeft = 0;
                slider.style.marginRight = Spacing;
                slider.RegisterValueChangedCallback(change =>
                {
                    ApplySerializedEdit(property, SliderEdit(change.previousValue, change.newValue, integer), limits, integer, true);
                    RefreshSlider();
                });
                input.Add(slider);
                root.TrackPropertyValue(min, _ => RefreshSlider());
                root.TrackPropertyValue(max, _ => RefreshSlider());
                root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += RefreshSlider);
                root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= RefreshSlider);
            }

            var maxInput = integer
                ? MakeNumberField(new IntegerField(), max, "Max", hasSlider, value => value, value => (int)value, CommitMax)
                : MakeNumberField(new FloatField(), max, "Max", hasSlider, value => value, value => (float)value, CommitMax);
            input.Add(maxInput);
            return root;

            double CommitMin(double value)
            {
                ApplySerializedEdit(property, new Edit(value, null), limits, integer, true);
                RefreshSlider();
                return ReadEndpoints(min, max, integer).Min;
            }

            double CommitMax(double value)
            {
                ApplySerializedEdit(property, new Edit(null, value), limits, integer, true);
                RefreshSlider();
                return ReadEndpoints(min, max, integer).Max;
            }

            void RefreshSlider()
            {
                if (slider != null)
                {
                    property.serializedObject.UpdateIfRequiredOrScript();
                    var current = ReadEndpoints(min, max, integer);
                    slider.SetValueWithoutNotify(new Vector2((float)current.Min, (float)current.Max));
                }
            }
        }

        private static VisualElement MakeNumberField<T>(TextValueField<T> field, SerializedProperty property,
            string caption, bool hasSlider, Func<T, double> read, Func<double, T> write, Func<double, double> commit)
        {
            field.isDelayed = true;
            field.labelElement.style.display = DisplayStyle.None;
            field.style.flexShrink = 1;
            field.style.minWidth = 0;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
            field.RegisterCallback<ChangeEvent<T>>(change =>
            {
                if (change.target == field)
                {
                    // Apply the endpoint per target before the native binding can
                    // broadcast its unconstrained field value to the selection.
                    change.StopImmediatePropagation();
                    field.SetValueWithoutNotify(write(commit(read(change.newValue))));
                    field.showMixedValue = property.hasMultipleDifferentValues;
                }
            }, TrickleDown.TrickleDown);
            field.BindProperty(property);

            if (hasSlider)
            {
                field.style.width = RangeFieldWidth;
                return field;
            }

            field.style.flexGrow = 1;
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.minWidth = 0;
            var label = new Label(caption);
            label.AddToClassList(BaseField<T>.labelDraggerVariantUssClassName);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginRight = 4f;
            label.style.minWidth = 28f;
            var dragger = new FieldMouseDragger<T>(field);
            dragger.SetDragZone(label);
            box.Add(label);
            box.Add(field);
            return box;
        }

        private static void ApplySerializedEdit(SerializedProperty property, Edit edit, Endpoints limits, bool integer, bool applyImmediately)
        {
            if (!edit.HasChange)
            {
                return;
            }

            var selection = property.serializedObject;
            if (!selection.isEditingMultipleObjects)
            {
                SetEndpoints(property, edit, limits, integer);
                if (applyImmediately)
                {
                    selection.ApplyModifiedProperties();
                }

                return;
            }

            var path = property.propertyPath;
            selection.ApplyModifiedProperties();
            foreach (var target in selection.targetObjects)
            {
                using var item = new SerializedObject(target);
                var range = item.FindProperty(path);
                if (range != null)
                {
                    SetEndpoints(range, edit, limits, integer);
                    item.ApplyModifiedProperties();
                }
            }

            selection.SetIsDifferentCacheDirty();
            selection.Update();
        }

        private static void SetEndpoints(SerializedProperty property, Edit edit, Endpoints limits, bool integer)
        {
            var min = property.FindPropertyRelative("_min");
            var max = property.FindPropertyRelative("_max");
            var old = ReadEndpoints(min, max, integer);
            var next = ApplyEdit(old, edit, limits);
            if (!old.Min.Equals(next.Min))
            {
                SetNumber(min, next.Min, integer);
            }

            if (!old.Max.Equals(next.Max))
            {
                SetNumber(max, next.Max, integer);
            }
        }

        private static Endpoints ReadEndpoints(SerializedProperty min, SerializedProperty max, bool integer)
        {
            return integer ? new Endpoints(min.intValue, max.intValue) : new Endpoints(min.floatValue, max.floatValue);
        }

        private static void SetNumber(SerializedProperty property, double number, bool integer)
        {
            if (integer)
            {
                property.intValue = (int)number;
            }
            else
            {
                property.floatValue = (float)number;
            }
        }

        private sealed class RangeField : BaseField<Vector2>
        {
            internal RangeField(string label, VisualElement input) : base(label, input)
            {
                AddToClassList(alignedFieldUssClassName);
            }
        }
#endif
    }
}
