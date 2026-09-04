using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.GameResources;
using _8floor.TimeManagement.Core.Scripts.Runtime.Loader;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Cheats
{
    public class GameplayCheatsBridge : MonoBehaviour
    {
        private static GameplayCheatsBridge _instance;

        [SerializeField] 
        private WinBridge _winBridge;
    
        [SerializeField] 
        private ResourceBaseSO[] _defaultCheatResources;

        public static IReadOnlyList<ResourceBaseSO> DefaultCheatResources =>
            _instance != null && _instance._defaultCheatResources != null
                ? _instance._defaultCheatResources
                : System.Array.Empty<ResourceBaseSO>();

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void FinishLevel()
        {
            _winBridge.ShowWinView();
        }

        public void GiveResources()
        {
            GameplayCheats.GiveResources(500);
        }
    }
}
