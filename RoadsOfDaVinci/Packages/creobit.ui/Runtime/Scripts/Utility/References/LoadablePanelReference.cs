using UnityEngine;

namespace Creobit.UI.Utility
{
    [System.Serializable]
    public class LoadablePanelReference
    {
        [field: SerializeField] public RectTransform Parent { get; private set; }
        [field: SerializeField] public PanelReference Reference { get; private set; }
    }
}