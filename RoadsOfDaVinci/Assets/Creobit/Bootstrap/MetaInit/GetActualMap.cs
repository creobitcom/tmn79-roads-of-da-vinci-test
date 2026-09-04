using System;
using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using UnityEngine;
using VContainer;

public class GetActualMap : MonoBehaviour
{
    [SerializeField] private List<MapGame> maps;
    [SerializeField, Inject] GameSwitcher gameSwitcher;
    
    public MapPageView GetMainMap()
    {
        return maps.First().MapSpotViewMain;
    }
    
    public MapPageView GetBonusMap()
    {
        return maps.First().MapSpotViewBonus;
    }
}

[Serializable]
public struct MapGame
{
    [SerializeField] public string GameName;
    [SerializeField] public MapPageView MapSpotViewMain;
    [SerializeField] public MapPageView MapSpotViewBonus;
}