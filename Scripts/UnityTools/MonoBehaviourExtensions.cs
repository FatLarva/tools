using UnityEngine;

namespace UnityTools
{
    public static class MonoBehaviourExtensions
    {
        public static T NullChecked<T>(this T monoBehaviour)
            where T : MonoBehaviour
        {
            return monoBehaviour == null ? null : monoBehaviour;
        }
    }
}