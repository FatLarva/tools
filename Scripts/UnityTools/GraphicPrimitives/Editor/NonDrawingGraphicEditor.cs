/// @creator: Slipp Douglas Thompson
/// @license: Public Domain per The Unlicense.  See <http://unlicense.org/>.
/// @purpose: A UnityEngine.UI.Graphic subclass that provides only raycast targeting, skipping all drawing.
/// @why: Because this functionality should be built-into Unity.
/// @usage: Add a `NonDrawingGraphic` component to the GameObject you want clickable, but without its own image/graphics.
/// @intended project path: Assets/Plugins/UnityEngine UI Extensions/NonDrawingGraphic.cs
/// @interwebsouce: https://gist.github.com/capnslipp/349c18283f2fea316369

using System;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityTools.GraphicPrimitives;

//Don't forget to put this file inside a 'Editor' folder
namespace Tools.EditorOnly.GraphicPrimitives
{
	[CanEditMultipleObjects, CustomEditor(typeof(NonDrawingGraphic), false)]
	public class NonDrawingGraphicEditor : GraphicEditor
	{
		public override void OnInspectorGUI()
		{
			base.serializedObject.Update();
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.PropertyField(base.m_Script, Array.Empty<GUILayoutOption>());
			EditorGUI.EndDisabledGroup();
			// skipping AppearanceControlsGUI
			base.RaycastControlsGUI();
			base.serializedObject.ApplyModifiedProperties();
		}
	}
}
