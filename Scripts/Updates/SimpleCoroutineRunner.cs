using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tools.Updates
{
    public sealed class SimpleCoroutineRunner : ICoroutineRunner, IDisposable
    {
        private readonly SimpleCoroutineRunnerComponent _runnerComponent;
        private bool _isDestroyed;

        public SimpleCoroutineRunner(string gameObjectName, Transform parent = null)
        {
            var innerGameObject = new GameObject(gameObjectName);
            innerGameObject.transform.SetParent(parent);

            _runnerComponent = innerGameObject.AddComponent<SimpleCoroutineRunnerComponent>();
            _runnerComponent.ComponentDestroyed += OnComponentDestroyed;
        }

        public void Dispose()
        {
            if (!_isDestroyed && _runnerComponent != null)
            {
                _runnerComponent.ComponentDestroyed -= OnComponentDestroyed;
                Object.Destroy(_runnerComponent.gameObject);

                _isDestroyed = true;
            }
        }
        
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (_isDestroyed)
            {
                return null;
            }
            
            return _runnerComponent.StartCoroutine(routine);
        }

        public void StopCoroutine(Coroutine coroutineToStop)
        {
            if (_isDestroyed)
            {
                return;
            }
            
            _runnerComponent.StopCoroutine(coroutineToStop);
        }

        public void StopAllCoroutines()
        {
            if (_isDestroyed)
            {
                return;
            }
            
            _runnerComponent.StopAllCoroutines();
        }

        private void OnComponentDestroyed()
        {
            _isDestroyed = true;
        }
    }
}
