using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Loader;
using System;
using Creobit.Localization;
using TMPro;
using UnityEngine;
using VContainer;

public class LevelNameView : MonoBehaviour, IDisposable
{

    [SerializeField] private TextMeshProUGUI _levelName;
    [SerializeField] private bool _showOnlyLevelNumber;

    private ILevelLoader _levelLoader;

    private LevelBaseSO _levelData => _levelLoader.LevelBaseSO.CurrentValue;

    [Inject]
    private void Construct(ILevelLoader levelLoader) 
    {

        _levelLoader = levelLoader;

    }

    private void Awake() 
    {

        _levelLoader.LevelLoaded += SetLevelName;

    }

    private void OnEnable() 
    {

        SetLevelName();

    }

    private void SetLevelName() 
    {

        if(_levelData == null)
        {
            return;
        }

        if (_showOnlyLevelNumber)
        {
            _levelName.text = _levelData.LevelNumber.ToString();
            return;
        }

        if (_levelData.OverrideLevelName)
        {
            _levelName.text = LocalizationService.Instance.GetText(_levelData.LevelName);
        }
        else
        {
            _levelName.text = string.Format(LocalizationService.Instance.GetText("game_txt_level"), 
                _levelData.LevelNumber);
        }

    }

    public void Dispose() 
    {

        _levelLoader.LevelLoaded -= SetLevelName;

    }

}
