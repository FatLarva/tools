using System.Collections.Generic;

namespace Tools.Extensions
{
    public static class ListExtensions
    {
        public static bool TryGetIndexOf<T>(this List<T> sourceList, T objectToFind, out int index)
        {
            index = sourceList.IndexOf(objectToFind);
            return index > -1;
        }
        
        public static bool TryGetValueAt<T>(this List<T> sourceList, int index, out T valueAtIndex)
        {
            if (0 <= index && index < sourceList.Count)
            {
                valueAtIndex = sourceList[index];
                return true;
            }

            valueAtIndex = default;
            return false;
        }
    }
}