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
