using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools.EditorOnly.Helpers
{
    [InitializeOnLoad]
    internal static class SelectInspectedAsset
    {
        private static readonly GUIContent Button = new("Select", "Select this asset in the Project window");

        static SelectInspectedAsset()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawButton;
        }

        private static void DrawButton(UnityEditor.Editor editor)
        {
            if (editor.targets.Length != 1)
            {
                return;
            }

            Object inspected = editor.target;
            if (inspected is AssetImporter importer)
            {
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(importer.assetPath);
                if (assetType != typeof(GameObject) && !typeof(ScriptableObject).IsAssignableFrom(assetType))
                {
                    return;
                }
            }
            // The prefab importer already draws the asset action above the GameObject inspector.
            else if (inspected is not ScriptableObject || !AssetDatabase.Contains(inspected))
            {
                return;
            }

            Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            row.xMin = Mathf.Max(row.xMin, row.xMax - EditorStyles.miniButton.CalcSize(Button).x);
            if (GUI.Button(row, Button, EditorStyles.miniButton))
            {
                Object asset = inspected is AssetImporter source
                    ? AssetDatabase.LoadMainAssetAtPath(source.assetPath)
                    : inspected;
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
