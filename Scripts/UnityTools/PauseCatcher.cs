using System;
using UnityEngine;

namespace UnityTools
{
    public class PauseCatcher : MonoBehaviour
    {
        public event Action<bool> ApplicationPaused;
        
        private void OnApplicationPause(bool pauseStatus)
        {
            ApplicationPaused?.Invoke(pauseStatus);
        }
    }
}
