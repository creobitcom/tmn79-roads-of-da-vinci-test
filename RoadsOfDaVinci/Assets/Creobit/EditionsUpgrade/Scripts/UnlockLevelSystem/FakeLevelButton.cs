using System;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    [AddComponentMenu("Creobit/EditionsUpgrade/FakeLevelButton")]
    public class FakeLevelButton : MonoBehaviour
    {
        public Button FakeButton { get; private set; }

        protected void Awake()
        {
            FakeButton = GetComponent<Button>();
        }
    }
}