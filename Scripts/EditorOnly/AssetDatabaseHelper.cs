using System;
using System.IO;
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
    }
}
