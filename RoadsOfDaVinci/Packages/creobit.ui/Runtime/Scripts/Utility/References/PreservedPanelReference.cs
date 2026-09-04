using UnityEngine;

namespace Creobit.UI.Utility
{
    [System.Serializable]
    public class PreservedPanelReference
    {
        [field: SerializeField] public int Layer { get; private set; }
        [field: SerializeField] public PanelReference Reference { get; private set; }
    }
}