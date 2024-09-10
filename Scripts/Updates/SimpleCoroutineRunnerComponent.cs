using System;
using UnityEngine;

namespace Tools.Updates
{
    public sealed class SimpleCoroutineRunnerComponent : MonoBehaviour
    {
        public event Action ComponentDestroyed;
        
        private void OnDestroy()
        {
            ComponentDestroyed?.Invoke();
            ComponentDestroyed = null;
        }
    }
}
