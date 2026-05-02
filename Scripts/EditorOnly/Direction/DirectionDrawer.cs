using UnityEngine;
using UnityEditor;
using UnityTools;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Tools.Editor
{
    [CustomPropertyDrawer(typeof(DirectionAttribute))]
    public sealed class DirectionPropertyDrawer : PropertyDrawer
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
                EditorGUI.HelpBox(position, "[Direction] is only valid on Vector2 fields.", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var dir = property.vector2Value;
            if (dir.sqrMagnitude < 1e-6f)
            {
                dir = Vector2.right;
            }

            EditorGUI.BeginChangeCheck();
            var result = DirectionDrawerHelper.Draw(position, label, dir);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector2Value = result;
            }

            EditorGUI.EndProperty();
        }
    }

#if ODIN_INSPECTOR
    public sealed class DirectionOdinDrawer : OdinAttributeDrawer<DirectionAttribute, Vector2>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var dir = ValueEntry.SmartValue;
            if (dir.sqrMagnitude < 1e-6f)
            {
                dir = Vector2.right;
            }

            var rect = EditorGUILayout.GetControlRect(true, DirectionDrawerHelper.TotalHeight);
            var displayLabel = label ?? new GUIContent(Property.NiceName);
            var result = DirectionDrawerHelper.Draw(rect, displayLabel, dir);
            if (result != ValueEntry.SmartValue)
            {
                ValueEntry.SmartValue = result;
            }
        }
    }
#endif

    internal static class DirectionDrawerHelper
    {
        public const float TotalHeight = 66f;
        private const float CompassSize = 56f;

        private static Texture2D _circleTex;
        private static Texture2D _arrowTex;

        public static Vector2 Draw(Rect position, GUIContent label, Vector2 dir)
        {
            var lineH = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var labelW = EditorGUIUtility.labelWidth;
            var contentX = position.x + labelW;

            EditorGUI.LabelField(
                new Rect(position.x, position.y + (TotalHeight - lineH) * 0.5f, labelW - 2f, lineH),
                label
            );

            // X / Y read-only column
            const float XyColW = 68f;
            var xyY = position.y + (TotalHeight - 2f * lineH - spacing) * 0.5f;
            var prevLW = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 13f;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.FloatField(new Rect(contentX, xyY, XyColW, lineH), new GUIContent("X"), dir.x);
                EditorGUI.FloatField(new Rect(contentX, xyY + lineH + spacing, XyColW, lineH), new GUIContent("Y"), dir.y);
            }

            EditorGUIUtility.labelWidth = prevLW;

            // Compass
            var compassX = contentX + XyColW + 4f;
            var compassRect = new Rect(compassX, position.y + (TotalHeight - CompassSize) * 0.5f, CompassSize, CompassSize);
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            dir = DrawCompass(compassRect, dir, angle, out angle);

            // Angle field
            var angleX = compassX + CompassSize + 4f;
            var angleFieldW = position.xMax - angleX - 2f;
            var angleY = position.y + (TotalHeight - lineH) * 0.5f;
            EditorGUIUtility.labelWidth = 38f;
            EditorGUI.BeginChangeCheck();
            var newAngle = EditorGUI.FloatField(
                new Rect(angleX, angleY, angleFieldW, lineH),
                new GUIContent("Angle"),
                angle
            );
            if (EditorGUI.EndChangeCheck())
            {
                newAngle = (newAngle % 360f + 360f) % 360f;
                var rad = newAngle * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }

            EditorGUIUtility.labelWidth = prevLW;

            return dir;
        }

        private static Vector2 DrawCompass(Rect rect, Vector2 dir, float angle, out float outAngle)
        {
            outAngle = angle;
            var e = Event.current;
            var controlId = GUIUtility.GetControlID(FocusType.Passive, rect);

            if (e.button == 0)
            {
                if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                }

                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && GUIUtility.hotControl == controlId)
                {
                    var delta = e.mousePosition - rect.center;
                    if (delta.sqrMagnitude > 9f)
                    {
                        // Screen Y is down; Vector2 Y is up — flip Y to convert.
                        dir = new Vector2(delta.x, -delta.y).normalized;
                        outAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        if (outAngle < 0f)
                        {
                            outAngle += 360f;
                        }

                        if (e.control)
                        {
                            var snap = e.shift ? 1f : 5f;
                            outAngle = Mathf.Round(outAngle / snap) * snap;
                            var rad = outAngle * Mathf.Deg2Rad;
                            dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        }

                        GUI.changed = true;
                    }

                    e.Use();
                }

                if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
            }

            if (e.type == EventType.Repaint)
            {
                GUI.DrawTexture(rect, GetCircleTex(), ScaleMode.StretchToFill, true);

                // Arrow texture points UP; rotate so it matches direction.
                // angle=0 (east) needs 90 CW, angle=90 (north) needs 0, etc.
                var savedMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(90f - angle, rect.center);
                GUI.DrawTexture(rect, GetArrowTex(), ScaleMode.StretchToFill, true);
                GUI.matrix = savedMatrix;
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.RotateArrow);
            return dir;
        }

        private static Texture2D GetCircleTex()
        {
            return _circleTex != null ? _circleTex : _circleTex = BuildCircleTex(64);
        }

        private static Texture2D GetArrowTex()
        {
            return _arrowTex != null ? _arrowTex : _arrowTex = BuildArrowTex(64);
        }

        private static Texture2D BuildCircleTex(int sz)
        {
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[sz * sz];

            float cx = sz * 0.5f, cy = sz * 0.5f;
            var outerR = sz * 0.5f - 0.5f;
            var ringW = 2.5f;

            var bgColor = new Color(0.12f, 0.18f, 0.18f, 0.95f);
            var ringColor = new Color(0.55f, 0.65f, 0.65f, 1f);

            for (var y = 0; y < sz; y++)
            {
                for (var x = 0; x < sz; x++)
                {
                    var dx = x + 0.5f - cx;
                    var dy = y + 0.5f - cy;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > outerR + 0.5f)
                    {
                        px[y * sz + x] = Color.clear;
                        continue;
                    }

                    var edgeAA = Mathf.Clamp01(outerR + 0.5f - dist);
                    var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - (outerR - ringW)) / ringW));
                    var c = Color.Lerp(bgColor, ringColor, t);
                    c.a = Mathf.Lerp(bgColor.a, ringColor.a, t) * edgeAA;
                    px[y * sz + x] = c;
                }
            }

            // Tick marks at cardinal directions.
            // Texture Y=0 is bottom; high Y = top of texture = top of screen in IMGUI.
            // N=90, E=0, S=270, W=180 in texture space (Y-up, same as Mathf.Atan2).
            var tickN = new Color(0.90f, 0.97f, 0.97f);
            var tickC = new Color(0.70f, 0.80f, 0.80f);
            AddTick(px, sz, cx, cy, outerR, ringW, 90f, tickN);
            AddTick(px, sz, cx, cy, outerR, ringW, 0f, tickC);
            AddTick(px, sz, cx, cy, outerR, ringW, 270f, tickC);
            AddTick(px, sz, cx, cy, outerR, ringW, 180f, tickC);

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static void AddTick(
            Color[] px,
            int sz,
            float cx,
            float cy,
            float outerR,
            float ringW,
            float angleDeg,
            Color color)
        {
            var rad = angleDeg * Mathf.Deg2Rad;
            float cosA = Mathf.Cos(rad), sinA = Mathf.Sin(rad);
            var inner = outerR - ringW - 1f;
            var outer2 = inner - 5f;
            const float HalfW = 1.2f;

            for (var y = 0; y < sz; y++)
            {
                for (var x = 0; x < sz; x++)
                {
                    var dx = x + 0.5f - cx;
                    var dy = y + 0.5f - cy;
                    var along = dx * cosA + dy * sinA;
                    var perp = Mathf.Abs(-dx * sinA + dy * cosA);
                    if (along >= outer2 && along <= inner && perp <= HalfW)
                    {
                        var idx = y * sz + x;
                        px[idx] = Color.Lerp(px[idx], color, 0.9f);
                    }
                }
            }
        }

        private static Texture2D BuildArrowTex(int sz)
        {
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[sz * sz];

            float cx = sz * 0.5f, cy = sz * 0.5f;
            var col = new Color(0.95f, 0.95f, 0.95f);

            // Arrow points toward HIGH texture Y = top of screen.
            var tipY = 0.87f * sz;
            var headBaseY = 0.68f * sz;
            var shaftBotY = 0.44f * sz;
            var headHW = 0.09f * sz;
            var shaftHW = 0.025f * sz;

            for (var y = 0; y < sz; y++)
            {
                for (var x = 0; x < sz; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    var a = 0f;

                    // Arrowhead: triangle, tip at (cx, tipY), base at headBaseY, half-width headHW.
                    if (fy >= headBaseY - 0.5f && fy <= tipY + 0.5f)
                    {
                        var t = Mathf.Clamp01((fy - headBaseY) / (tipY - headBaseY));
                        var hw = headHW * (1f - t);
                        a = Mathf.Max(a, Mathf.Clamp01(0.5f - (Mathf.Abs(fx - cx) - hw)));
                    }

                    // Shaft
                    if (fy >= shaftBotY - 0.5f && fy <= headBaseY + 0.5f)
                    {
                        a = Mathf.Max(a, Mathf.Clamp01(0.5f - (Mathf.Abs(fx - cx) - shaftHW)));
                    }

                    px[y * sz + x] = new Color(col.r, col.g, col.b, a);
                }
            }

            // Center dot
            const float DotR = 2.5f;
            for (var y = 0; y < sz; y++)
            {
                for (var x = 0; x < sz; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < DotR + 0.5f)
                    {
                        var idx = y * sz + x;
                        var a = Mathf.Clamp01(DotR + 0.5f - dist);
                        px[idx] = new Color(col.r, col.g, col.b, Mathf.Max(px[idx].a, a));
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}