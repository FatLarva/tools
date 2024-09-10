using System;
using UnityEngine;

namespace UnityTools
{
    public sealed class DestroyableComponent : MonoBehaviour
    {
        public event Action ComponentDestroyed;
        
        private void OnDestroy()
        {
            ComponentDestroyed?.Invoke();
            ComponentDestroyed = null;
        }
    }
}
