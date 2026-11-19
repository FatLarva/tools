using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PreviousValues = System.Collections.Generic.Dictionary<(UnityEngine.Object target, string path), (object value, uint arrayHash)>;

namespace Tools.Editor
{
    [CustomPropertyDrawer(typeof(Optional<>))]
    internal sealed class OptionalDrawer : PropertyDrawer, IDisposable
    {
        private const string UnsupportedType = "Unity cannot serialize this value type.";
        private PreviousValues _imguiValues;

        public void Dispose()
        {
            _imguiValues = null;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var enabled = property.FindPropertyRelative("_hasValue");
            var value = property.FindPropertyRelative("_value");
            string caption = preferredLabel ?? property.displayName;

            var row = new VisualElement { tooltip = property.tooltip };
            row.style.flexDirection = FlexDirection.Row;

            var toggle = new PresenceToggle { bindingPath = enabled.propertyPath, tooltip = caption };
            toggle.style.alignSelf = Align.FlexStart;
            toggle.style.flexShrink = 0;
            row.Add(toggle);

            var label = new Label(caption);
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);

            VisualElement field = value != null
                ? new PropertyField(value, caption)
                : new Label($"{caption}  {UnsupportedType}");
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            row.Add(field);

            void ShowValue(bool showValue)
            {
                label.style.display = showValue ? DisplayStyle.None : DisplayStyle.Flex;
                field.style.display = showValue ? DisplayStyle.Flex : DisplayStyle.None;
            }

            ShowValue(enabled.boolValue && !enabled.hasMultipleDifferentValues);
            toggle.PresenceChanged += ShowValue;
            PreviousValues previous = null;
            toggle.RegisterCallback<ChangeEvent<bool>>(change =>
            {
                SetPresence(property, change.newValue, previous ??= new());
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == row)
                {
                    previous = null;
                }
            });
            return row;
        }

        // Unity also updates bound fields silently when an edit is undone or a mixed selection changes.
        private sealed class PresenceToggle : Toggle
        {
            public event Action<bool> PresenceChanged;

            public override void SetValueWithoutNotify(bool newValue)
            {
                base.SetValueWithoutNotify(newValue);
                PresenceChanged?.Invoke(newValue && !showMixedValue);
            }

            protected override void UpdateMixedValueContent()
            {
                base.UpdateMixedValueContent();
                PresenceChanged?.Invoke(value && !showMixedValue);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var enabled = property.FindPropertyRelative("_hasValue");
            if (enabled.boolValue && !enabled.hasMultipleDifferentValues)
            {
                var value = property.FindPropertyRelative("_value");
                if (value != null)
                {
                    return EditorGUI.GetPropertyHeight(value, label, true);
                }
            }
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            var enabled = property.FindPropertyRelative("_hasValue");
            Rect content = EditorGUI.IndentedRect(position);
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                float size = EditorGUIUtility.singleLineHeight;
                var toggle = new Rect(content.x, content.y, size, size);
                EditorGUI.BeginChangeCheck();
                bool present;
                using (new EditorGUI.MixedValueScope(enabled.hasMultipleDifferentValues))
                {
                    present = EditorGUI.Toggle(toggle, enabled.boolValue);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    SetPresence(property, present, _imguiValues ??= new());
                }
                content.xMin = toggle.xMax + EditorGUIUtility.standardVerticalSpacing;

                if (enabled.boolValue && !enabled.hasMultipleDifferentValues)
                {
                    var value = property.FindPropertyRelative("_value");
                    if (value != null)
                    {
                        EditorGUI.PropertyField(content, value, label, true);
                    }
                    else
                    {
                        EditorGUI.LabelField(content, label, new GUIContent(UnsupportedType));
                    }
                }
                else
                {
                    EditorGUI.LabelField(content, label);
                }
            }
            EditorGUI.EndProperty();
        }

        private static void SetPresence(SerializedProperty property, bool present, PreviousValues previous)
        {
            var selection = property.serializedObject;
            string path = property.propertyPath;
            int arrayIndex = path.IndexOf(".Array.data[", StringComparison.Ordinal);
            selection.ApplyModifiedProperties();
            foreach (var target in selection.targetObjects)
            {
                using var data = new SerializedObject(target);
                var optional = data.FindProperty(path);
                var flag = optional.FindPropertyRelative("_hasValue");
                if (flag.boolValue == present)
                {
                    continue;
                }

                // A reordered list can now have a different item at the same property path.
                var array = arrayIndex < 0 ? null : data.FindProperty(path[..arrayIndex]);
                var key = (target, path);
                if (present)
                {
                    if (previous.Remove(key, out var saved) && saved.arrayHash == (array?.contentHash ?? 0u))
                    {
                        optional.boxedValue = saved.value;
                    }
                    flag.boolValue = true;
                }
                else
                {
                    object value = optional.boxedValue;
                    optional.boxedValue = Activator.CreateInstance(value.GetType());
                    previous[key] = (value, array?.contentHash ?? 0u);
                }
                data.ApplyModifiedProperties();
            }
            selection.SetIsDifferentCacheDirty();
            selection.Update();
        }
    }
}
