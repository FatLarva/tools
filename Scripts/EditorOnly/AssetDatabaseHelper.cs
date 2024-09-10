using System;
using System.IO;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Tools.EditorOnly
{
    public static class AssetDatabaseHelper
    {
        public static string AssetsRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return ("Assets" + absolutePath.Substring(Application.dataPath.Length)).NormalizeSlashes('/');
            }
            
            throw new ArgumentException("Full path does not contain the current project's Assets folder", nameof(absolutePath));
        }
        
        public static string AbsolutePath(string assetsRelativePath)
        {
	        var dataPath = Application.dataPath.NormalizeSlashes('/').TrimEnd('/').Remove("/Assets");
	        var fullPath = Path.Combine(dataPath, assetsRelativePath).NormalizeSlashes('/');

	        return fullPath;
        }
        
        [MenuItem("Assets/MultiRename")]
        private static void MultiRename()
        {
	        var operations = new MultiRenameOperations(); 
	        var modalWindow = OdinEditorWindow.CreateOdinEditorWindowInstanceForObject(operations);
	        modalWindow.position = GUIHelper.GetEditorWindowRect().AlignCenter(300, 300);
	        modalWindow.ShowModalUtility();
	        
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

                AssetDatabase.RenameAsset(assetPath, newFileName);
        	}

            AssetDatabase.Refresh();
        }

        private class MultiRenameOperations
        {
	        public string StringToRemove;
	        public string StringToAdd;
        }
    }
}
