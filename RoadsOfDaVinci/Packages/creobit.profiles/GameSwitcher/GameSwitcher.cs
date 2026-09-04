using System;
using System.Collections.Generic;
using System.Linq;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Localization;
using R3;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

public class GameSwitcher : MonoBehaviour
{
    [SerializeField] private List<GameData> _gameDatas;
    public IReadOnlyList<GameData> GameDatas => _gameDatas;
    [field: SerializeField] public SerializableReactiveProperty<GameData> CurrentGame { get; private set; }
    [Inject] private readonly IPlayerPrefsSaveProvider _playerPrefsSaveProvider;
    
    private void Awake()
    {
        if (PlayerPrefs.HasKey("LastGame"))
        {
            CurrentGame.Value = _gameDatas.First(x => x.GameName.Contains(PlayerPrefs.GetString("LastGame")));
        }
        DontDestroyOnLoad(this);
    }

    public async void SetCurrentGame(GameData game)
    {
        CurrentGame.Value = game;
        var language = _playerPrefsSaveProvider.TryGetValue("Language", "en-US");
        await LocalizationService.Instance.Init(CurrentGame.Value.GameName, language);
        PlayerPrefs.SetString("LastGame", game.GameName);
        PlayerPrefs.Save();
    }
    
    public async void SetCurrentGame(string name)
    {
        CurrentGame.Value = _gameDatas.First(x=>x.GameName == name);
        var language = _playerPrefsSaveProvider.TryGetValue("Language", "en-US");
        await LocalizationService.Instance.Init(CurrentGame.Value.GameName, language);
        PlayerPrefs.SetString("LastGame", name);
        PlayerPrefs.Save();
    }
}