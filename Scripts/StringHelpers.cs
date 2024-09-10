using System.IO;
using System.Linq;

namespace Tools
{
    public static class StringHelpers
    {
        public static string NormalizeSlashes(this string pathToNormalize, char targetSlash)
        {
            if (targetSlash == '/')
            {
                return pathToNormalize.Replace('\\', '/');
            }
            else
            {
                return pathToNormalize.Replace('/', '\\');
            }
        }

        public static string Capitalize(this string sourceString)
        {
            return char.ToUpperInvariant(sourceString[0]) + sourceString.Substring(1);
        }
        
        public static string Decapitalize(this string sourceString)
        {
            return char.ToLowerInvariant(sourceString[0]) + sourceString.Substring(1);
        }
        
        public static string ToPascalCase(this string sourceString)
        {
            if (!sourceString.Contains(' ') && !sourceString.Contains('_'))
            {
                return sourceString;
            }

            var pieces = sourceString.Split(' ', '_').Where(piece => !string.IsNullOrEmpty(piece)).Select(piece => piece.Capitalize()).ToArray();
            
            return string.Join(string.Empty, pieces);
        }
        
        public static string RemoveZanyCharacters(this string sourceString)
        {
            var resultString = string.Concat(sourceString.Split(Path.GetInvalidFileNameChars()));
            return resultString.Remove("'").Remove(":").Remove("`").Remove("~");
        }
        
        public static string Remove(this string sourceString, string substringToRemove)
        {
            return sourceString.Replace(substringToRemove, "");
        }
    }
}
