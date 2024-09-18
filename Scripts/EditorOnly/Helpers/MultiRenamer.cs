using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tools.EditorOnly.Helpers
{
    public class MultiRenamer
    {
        [MenuItem("Assets/MultiRename")]
        private static void MultiRename()
        {
	        var operations = new MultiRenameOperations();
	        
	        var settings = new ModalEditorWindow.ModalEditorWindowSettings
	        {
				Title = "MultiRename",
				Message = "You can add a common string to the names of all selected assets or remove all instances of the common string from the names of all selected assets (NO UNDO!)",
				ConfirmText = "Rename",
				OnConfirm = () => operations.IsConfirmed = true,
				OnGuiLayoutContent = () =>
				{
					operations.StringToAdd = EditorGUILayout.TextField("String to add", operations.StringToAdd);
					operations.StringToRemove = EditorGUILayout.TextField("String to remove", operations.StringToRemove);
					
					EditorGUILayout.LabelField("Replace");
					
					GUILayout.BeginHorizontal();

					operations.StringToReplace = EditorGUILayout.TextField(operations.StringToReplace);
					operations.StringToReplaceWith = EditorGUILayout.TextField(operations.StringToReplaceWith);
					
					GUILayout.EndHorizontal();
				},
	        };
	        ModalEditorWindow.ShowModalWindow(settings);

	        if (!operations.IsConfirmed)
	        {
		        return;
	        }
	        
        	for (int i = 0; i < Selection.count; i++)
        	{
        		var selectedObject = Selection.objects[i];
        		
        		var assetPath = AssetDatabase.GetAssetPath(selectedObject);
                var fileName = Path.GetFileName(assetPath);
                var newFileName = fileName;

                if (!string.IsNullOrEmpty(operations.StringToRemove))
                {
	                newFileName = newFileName.Remove(operations.StringToRemove);
                }
                
                if (!string.IsNullOrEmpty(operations.StringToAdd))
                {
	                var fileNameNoExtension = Path.GetFileNameWithoutExtension(newFileName);
	                var fileExtension = Path.GetExtension(newFileName);
	                newFileName = $"{fileNameNoExtension}{operations.StringToAdd}{fileExtension}";
                }
                
                if (!string.IsNullOrEmpty(operations.StringToReplace))
                {
	                newFileName = newFileName.Replace(operations.StringToReplace, operations.StringToReplaceWith, StringComparison.Ordinal);
                }
				
                AssetDatabase.RenameAsset(assetPath, newFileName);
        	}
	        
	        AssetDatabase.SaveAssets();
	        AssetDatabase.Refresh();
        }

        private class MultiRenameOperations
        {
	        public string StringToRemove;
	        public string StringToAdd;
	        public string StringToReplace;
	        public string StringToReplaceWith;
	        public bool IsConfirmed;
        }
    }
}