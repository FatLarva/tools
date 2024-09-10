using JetBrains.Annotations;
using UnityEngine;

namespace UnityTools
{
	public class DontDestroyOnLoad : MonoBehaviour
	{
		[UsedImplicitly]
		private void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}
	}
}
