using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tools.EditorOnly.Textures
{
	public static class TextureWhitener
	{
		[MenuItem("Assets/WhitenTexture")]
		private static void WhitenTexture()
		{
			for (int i = 0; i < Selection.count; i++)
			{
				var selectedObject = Selection.objects[i];
				
				if (!(selectedObject is Texture2D sourceTexture))
				{
					Debug.LogError($"{nameof(selectedObject)} is not a texture. This method can be called only on textures.");
					return;
				}
			
				var textureAssetPath = AssetDatabase.GetAssetPath(sourceTexture);
				MakeTextureWhite(textureAssetPath);
			}

			AssetDatabase.Refresh();
		}
		
		private static void MakeTextureWhite(string textureAssetPath)
		{
			var fileName = Path.GetFileNameWithoutExtension(textureAssetPath);
			var folderName = Path.GetDirectoryName(textureAssetPath) ?? string.Empty;
			var newFileName = Path.Combine(folderName, fileName) + "_whitenned" + ".png";

			var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
			var isSourceWasReadableBefore = sourceImporter.isReadable;
			if (!isSourceWasReadableBefore)
			{
				sourceImporter.isReadable = true;
				sourceImporter.SaveAndReimport();
			}
			
			AssetDatabase.CopyAsset(textureAssetPath, newFileName);

			Texture2D newTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(newFileName);
			var pixels = newTexture.GetPixels();

			for (int i = 0; i < pixels.Length; i++)
			{
				var oldColor = pixels[i];
				pixels[i] = new Color(1.0f, 1.0f, 1.0f, oldColor.a);
			}

			newTexture.SetPixels(pixels);
			newTexture.Apply();

			var pngBytes = newTexture.EncodeToPNG();
			File.WriteAllBytes(newFileName, pngBytes);

			AssetDatabase.ImportAsset(newFileName, ImportAssetOptions.ForceUpdate);

			if (!isSourceWasReadableBefore)
			{
				sourceImporter.isReadable = false;
				sourceImporter.SaveAndReimport();
			}
			
			var targetImporter = AssetImporter.GetAtPath(newFileName);

			EditorUtility.CopySerialized(sourceImporter, targetImporter);
			targetImporter.SaveAndReimport();

			EditorUtility.SetDirty(newTexture);
			AssetDatabase.SaveAssetIfDirty(newTexture);
		}

		// Note that we pass the same path, and also pass "true" to the second argument.
		[MenuItem("Assets/WhitenTexture", true)]
		private static bool IsValidTextureToBeWhiten()
		{
			if (Selection.activeObject == null)
			{
				return false;
			}

			if (Selection.count == 1 && Selection.activeObject is Texture2D)
			{
				return true;
			}

			if (Selection.count > 1)
			{
				var areAllSelectedAssetsTextures = true;
				
				for (int i = 0; i < Selection.count; i++)
				{
					var selectedObject = Selection.objects[i];
					if (!(selectedObject is Texture2D))
					{
						areAllSelectedAssetsTextures = false;

						break;
					}
				}

				return areAllSelectedAssetsTextures;
			}

			return false;
		}
	}
}
