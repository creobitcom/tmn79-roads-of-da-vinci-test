using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Creobit.UI.Utility
{
    public class PanelReference : ScriptableObject
    {
        [field: SerializeField]
        public AssetReference UIPanelReference { get; set; }
    }
}