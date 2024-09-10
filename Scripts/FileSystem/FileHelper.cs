using System;
using System.IO;

namespace Tools.FileSystem
{
    public static class FileHelper
    {
        public static bool IsAcceptableFileExtension(string filePath, bool caseSensitive, string acceptableExtension)
        {
            acceptableExtension = PrepareAndValidateExtension(acceptableExtension);

            if (CheckEmptyExtensionAccepted(filePath, acceptableExtension))
            {
                return true;
            }
            
            var fileExtension = Path.GetExtension(filePath).Substring(1);
            var stringComparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            return string.Equals(acceptableExtension, fileExtension, stringComparison);
        }
        
        public static bool IsAcceptableFileExtension(string filePath, bool caseSensitive, string acceptableExtension0, string acceptableExtension1)
        {
            return IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension0)
                   || IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension1);
        }
        
        public static bool IsAcceptableFileExtension(string filePath, bool caseSensitive, string acceptableExtension0, string acceptableExtension1, string acceptableExtension2)
        {
            return IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension0)
                   || IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension1)
                   || IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension2);
        }
        
        public static bool IsAcceptableFileExtension(string filePath, bool caseSensitive, params string[] acceptableExtensions)
        {
            foreach (var acceptableExtension in acceptableExtensions)
            {
                if (IsAcceptableFileExtension(filePath, caseSensitive, acceptableExtension))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckEmptyExtensionAccepted(string filePath, string acceptableExtension)
        {
            var hasExtension = Path.HasExtension(filePath);
            if (acceptableExtension == string.Empty && !hasExtension)
            {
                return true;
            }

            return false;
        }

        private static string PrepareAndValidateExtension(string extension)
        {
            if (extension == null)
            {
                throw new ArgumentException("Provided extension cannot be null, but can be empty string.", nameof(extension));
            }

            if (extension == string.Empty)
            {
                return extension;
            }
            
            var resultExtension = extension;

            if (resultExtension[0] == '.')
            {
                resultExtension = resultExtension[1..];
            }

            if (resultExtension.Contains('.'))
            {
                throw new ArgumentException("Extension cannot contain more than one dots. And this dot can only be trailing dot from the left.", nameof(extension));
            }
            
            return resultExtension;
        }
    }
}
