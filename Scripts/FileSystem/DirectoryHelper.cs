using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tools.FileSystem
{
    public static class DirectoryHelper
    {
        private static readonly object OverwriteLock = new();

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
        
        /// <summary>Replaces the destination with the immediate files of the source. The existing destination is
        /// kept until all files are staged. With move semantics, staging is rolled back on failure.</summary>
        public static void OverwriteFilesInFolder(string sourceFolder, string destinationFolder, bool shouldKeepSourceIntact)
        {
            lock (OverwriteLock)
            {
                var source = NormalizeDirectoryPath(sourceFolder);
                var destination = NormalizeDirectoryPath(destinationFolder);
                var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (IsSameOrBelow(source, destination, comparison) || IsSameOrBelow(destination, source, comparison))
                {
                    throw new ArgumentException("The source and destination directories must not overlap.", nameof(destinationFolder));
                }

                var parent = Path.GetDirectoryName(destination);
                if (parent == null)
                {
                    throw new ArgumentException("The destination must not be a filesystem root.", nameof(destinationFolder));
                }

                var files = Directory.GetFiles(source);
                RejectLinkedPath(source);
                RejectLinkedPath(destination);
                StageAndReplace(files, destination, parent, shouldKeepSourceIntact);
            }
        }

        private static void StageAndReplace(string[] files, string destination, string parent, bool copy)
        {
            var work = Path.Combine(parent, ".tools-overwrite-" + Guid.NewGuid().ToString("N"));
            var staged = Path.Combine(work, "staged");
            var previous = Path.Combine(work, "previous");
            var movedFiles = new List<(string Source, string Staged)>();
            bool hasBackup = false;
            bool keepRecoveryFiles = false;
            try
            {
                Directory.CreateDirectory(staged);
                foreach (var file in files)
                {
                    var target = Path.Combine(staged, Path.GetFileName(file));
                    if (copy)
                    {
                        File.Copy(file, target);
                    }
                    else
                    {
                        File.Move(file, target);
                        movedFiles.Add((file, target));
                    }
                }

                if (Directory.Exists(destination))
                {
                    Directory.Move(destination, previous);
                    hasBackup = true;
                }

                Directory.Move(staged, destination);
            }
            catch (Exception original)
            {
                var errors = new List<Exception> { original };
                if (hasBackup)
                {
                    TryRestore(() => Directory.Move(previous, destination), errors);
                }

                foreach (var file in movedFiles)
                {
                    TryRestore(() => File.Move(file.Staged, file.Source), errors);
                }

                if (errors.Count > 1)
                {
                    keepRecoveryFiles = true;
                    throw new AggregateException($"The folder replacement could not be fully rolled back. Recovery files are in '{work}'.", errors);
                }

                throw;
            }
            finally
            {
                if (!keepRecoveryFiles)
                {
                    TryRemoveWorkDirectory(work);
                }
            }
        }

        private static bool IsSameOrBelow(string directory, string path, StringComparison comparison)
        {
            var last = directory[directory.Length - 1];
            var prefix = last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar
                ? directory
                : directory + Path.DirectorySeparatorChar;
            return string.Equals(directory, path, comparison) || path.StartsWith(prefix, comparison);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed.Length < Path.GetPathRoot(fullPath).Length ? fullPath : trimmed;
        }

        private static void RejectLinkedPath(string path)
        {
            for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
            {
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"A linked directory cannot be used to replace files safely: '{directory.FullName}'.");
                }
            }
        }

        private static void TryRestore(Action restore, List<Exception> errors)
        {
            try
            {
                restore();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        private static void TryRemoveWorkDirectory(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A cleanup failure after a successful replacement must not signal that the replacement failed.
            }
        }
        
        public static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
    }
}
