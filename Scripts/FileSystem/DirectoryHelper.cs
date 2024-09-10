using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tools.FileSystem
{
    public static class DirectoryHelper
    {
        public static string GetMostUsedExtension(string folder, string[] ignoreExtensions = null)
        {
            var files = Directory.GetFiles(folder);

            return GetMostUsedExtension(files, ignoreExtensions);
        }
	
        public static string GetMostUsedExtension(string[] files, string[] ignoreExtensions = null)
        {
            if (files == null || files.Length == 0)
            {
                return null;
            }
		
            var extensionToCountMap = new Dictionary<string, int>(1);
		
            foreach (string file in files)
            {
                var extension = Path.GetExtension(file).Substring(1);

                if (ignoreExtensions != null && Array.IndexOf(ignoreExtensions, extension) > -1)
                {
                    continue;
                }
                
                if (!extensionToCountMap.TryGetValue(extension, out int count))
                {
                    count = 0;
                    extensionToCountMap[extension] = count;
                }

                count++;
                extensionToCountMap[extension] = count;
            }

            if (extensionToCountMap.Count == 0)
            {
                return null;
            }
		
            var mostUsedExtension = extensionToCountMap.Aggregate((kvp0, kvp1) => kvp0.Value > kvp1.Value ? kvp0 : kvp1).Key;

            return mostUsedExtension;
        }
        
        public static void CopyFilesBetweenFolders(string sourceFolder, string targetFolder, bool overwrite = false, Predicate<string> filter = null)
        {
            var files = Directory.GetFiles(sourceFolder);
            foreach (var file in files)
            {
                if (filter != null && !filter.Invoke(file))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetFolder, fileName);
                
                File.Copy(file, targetFile, overwrite);
            }
        }
        
        public static void MoveFilesBetweenFolders(string sourceFolder, string targetFolder, Predicate<string> filter = null)
        {
            var files = Directory.GetFiles(sourceFolder);
            foreach (var file in files)
            {
                if (filter != null && !filter.Invoke(file))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetFolder, fileName);
                
                File.Move(file, targetFile);
            }
        }
        
        public static void OverwriteFilesInFolder(string sourceFolder, string destinationFolder, bool shouldKeepSourceIntact)
        {
            if (Directory.Exists(destinationFolder))
            {
                Directory.Delete(destinationFolder, true);
            }

            Directory.CreateDirectory(destinationFolder);
                
            if (shouldKeepSourceIntact)
            {
                CopyFilesBetweenFolders(sourceFolder, destinationFolder, true);
            }
            else
            {
                MoveFilesBetweenFolders(sourceFolder, destinationFolder);
            }
        }
        
        public static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
    }
}
