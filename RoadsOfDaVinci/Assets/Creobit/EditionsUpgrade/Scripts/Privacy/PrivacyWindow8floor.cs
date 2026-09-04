using System;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
	[AddComponentMenu("Creobit/EditionsUpgrade/PrivacyWindow8floor")]
	public class PrivacyWindow8floor : MonoBehaviour
	{
		public event Action OnAccept;
		public void Agree()
		{
			OnAccept?.Invoke();
		}
	}
}