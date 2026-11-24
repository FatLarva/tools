/// @creator: Slipp Douglas Thompson
/// @license: Public Domain per The Unlicense.  See <http://unlicense.org/>.
/// @purpose: A UnityEngine.UI.Graphic subclass that provides only raycast targeting, skipping all drawing.
/// @why: Because this functionality should be built-into Unity.
/// @usage: Add a `NonDrawingGraphic` component to the GameObject you want clickable, but without its own image/graphics.
/// @intended project path: Assets/Plugins/UnityEngine UI Extensions/NonDrawingGraphic.cs
/// @interwebsouce: https://gist.github.com/capnslipp/349c18283f2fea316369

using UnityEditor;
using UnityTools.GraphicPrimitives;

namespace Tools.EditorOnly.GraphicPrimitives
{
    [CustomEditor(typeof(NonDrawingGraphic)), CanEditMultipleObjects]
    public class NonDrawingGraphicEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PrimitiveInspector.DrawInput(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
