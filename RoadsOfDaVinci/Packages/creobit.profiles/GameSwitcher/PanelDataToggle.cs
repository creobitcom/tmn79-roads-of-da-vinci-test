using System.Collections.Generic;
using System.Linq;
using Creobit.UI;
using UnityEngine;
using VContainer;

public class PanelDataToggle : MonoBehaviour
{
    [SerializeField, Inject] private GameSwitcher _gameSwitcher;
    [SerializeField] private List<PanelGameStruct> _panelGames;

    public PanelData GetPanel()
    {
        return _panelGames.First(x=>x.PanelName.Equals(_gameSwitcher.CurrentGame.Value.GameName)).PanelData;
    }
}