using UnityEngine;

namespace Creobit.UI.Utility
{
    [System.Serializable]
    public class ScenePanelReference
    {
        [field: SerializeField] public PanelData Data { get; private set; }
        [field: SerializeField] public PanelReference Reference { get; private set; }
    }
}