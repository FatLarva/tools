using System;

namespace Tools.Extensions
{
    public static class ArrayExtensions
    {
        public static bool TryGetIndexOf<T>(this T[] sourceArray, T objectToFind, out int index)
        {
            index = Array.IndexOf(sourceArray, objectToFind);
            return index > -1;
        }
        
        public static bool TryGetValueAt<T>(this T[] sourceList, int index, out T valueAtIndex)
        {
            if (0 <= index && index < sourceList.Length)
            {
                valueAtIndex = sourceList[index];
                return true;
            }

            valueAtIndex = default;
            return false;
        }
    }
}