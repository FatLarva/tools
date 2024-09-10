using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityTools;

namespace Tools.EditorOnly.Textures
{
	public static class TextureRounder
	{
		private const float AntiAliasingThickness = 2.5f;

		[MenuItem("Assets/MakeImageCircular")]
		private static void MakeImagesCircular()
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
				MakeTextureAssetCircledPNGCopy(textureAssetPath);
			}

			AssetDatabase.Refresh();
		}
		
		[MenuItem("Assets/MakeImageCircular", true)]
		private static bool IsValidTextureToBeRounded()
		{
			if (Selection.activeObject == null)
			{
				return false;
			}

			if (Selection.count == 1 && Selection.activeObject is Texture2D sourceTexture)
			{
				return sourceTexture.width == sourceTexture.height;
			}

			if (Selection.count > 1)
			{
				var isAllRoundTextures = true;
				
				for (int i = 0; i < Selection.count; i++)
				{
					var selectedObject = Selection.objects[i];
					if (!(selectedObject is Texture2D selectedObjectAsTexture))
					{
						isAllRoundTextures = false;

						break;
					}

					if (selectedObjectAsTexture.width != selectedObjectAsTexture.height)
					{
						isAllRoundTextures = false;

						break;
					}
				}

				return isAllRoundTextures;
			}

			return false;
		}

		public static void MakeTextureAssetCircledPNGCopy(string textureAssetPath, string copyAssetPath = null)
		{
			Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);

			string newFileName;
			
			if (string.IsNullOrWhiteSpace(copyAssetPath))
			{
				var fileName = Path.GetFileNameWithoutExtension(textureAssetPath);
				var folderName = Path.GetDirectoryName(textureAssetPath) ?? string.Empty;
				newFileName = Path.Combine(folderName, fileName) + "_circled" + ".png";
			}
			else
			{
				var fileName = Path.GetFileNameWithoutExtension(copyAssetPath);
				var folderName = Path.GetDirectoryName(copyAssetPath) ?? string.Empty;
				newFileName = Path.Combine(folderName, fileName) + ".png";
			}

			var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(textureAssetPath);
			var isSourceWasReadableBefore = sourceImporter.isReadable;
			if (!isSourceWasReadableBefore)
			{
				sourceImporter.isReadable = true;
				sourceImporter.SaveAndReimport();
			}

			var pngTexture = MakeCircleShapedTextureCopy(sourceTexture, DefaultFormat.LDR, AntiAliasingThickness);

			var pngBytes = pngTexture.EncodeToPNG();
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

			EditorUtility.SetDirty(pngTexture);
			AssetDatabase.SaveAssetIfDirty(pngTexture);
		}

		private static Texture2D MakeCircleShapedTextureCopy(Texture2D sourceTexture, DefaultFormat newTextureDefaultFormat, float antiAliasingThickness)
		{
			var pngTexture = new Texture2D(sourceTexture.width, sourceTexture.height, newTextureDefaultFormat, TextureCreationFlags.DontInitializePixels);
			var pixels = sourceTexture.GetPixels();

			var radius = (sourceTexture.width / 2.0f) - antiAliasingThickness;
			var radiusSqr = radius * radius;
			var center = new Vector2(radius, radius);

			for (int i = 0; i < pixels.Length; i++)
			{
				var texturePoint = new Vector2Int(i % sourceTexture.width, i / sourceTexture.width);
				var distanceSqr = (texturePoint - center).sqrMagnitude;

				if (distanceSqr < radiusSqr)
				{
					continue;
				}

				var distance = Mathf.Sqrt(distanceSqr);
				var distanceFromRadius = distance - radius;

				if (distanceFromRadius < antiAliasingThickness)
				{
					var t = distanceFromRadius / antiAliasingThickness;
					var pixelAlpha = Mathf.Lerp(1.0f, 0.0f, t);

					pixels[i] = pixels[i].WithAlpha(pixelAlpha);
				}
				else
				{
					pixels[i] = Color.clear;
				}
			}

			pngTexture.SetPixels(pixels);

			return pngTexture;
		}
	}
}
