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

        public static T GetOrAdd<T>(this GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component == null ? gameObject.AddComponent<T>() : component;
        }
    }
}