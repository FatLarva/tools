using System.Collections;
using UnityEngine;

namespace Tools.Updates
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator routine);

        void StopCoroutine(Coroutine coroutineToStop);

        void StopAllCoroutines();
    }
}
