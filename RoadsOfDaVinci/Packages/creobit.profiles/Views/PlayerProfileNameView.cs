using System;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Localization;
using TMPro;
using UnityEngine;
using VContainer;

public class PlayerProfileNameView : MonoBehaviour
{

    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private string _prefix;
    [SerializeField] private string _suffix;
    [SerializeField] private string _formatLocalizationKey;

    private string _localizedPrefix;
    private string _localizedSuffix;
    private string _localizedFormat;

    private IPlayerProfilesController _profilesController;

    [Inject]
    private void Construct(IPlayerProfilesController profilesController)
    {
        _profilesController = profilesController;
    }

    private void Start()
    {
        _profilesController.Service.SelectedProfile += SetProfileName;
        LocalizationService.Instance.OnLanguageChanged += UpdateProfileName;

        SetLocalization();

        SetProfileName(_profilesController.Service.CurrentProfile);
    }

    private void SetProfileName(PlayerProfileData playerProfileData)
    {
        if(string.IsNullOrEmpty(_localizedFormat) == false)
        {
            try
            {
                _nameText.text = string.Format(_localizedFormat, playerProfileData.Name);
            }
            catch(FormatException)
            {
                _nameText.text = _localizedFormat;
            }

            return;
        }

        _nameText.text = _localizedPrefix + playerProfileData.Name + _localizedSuffix;
    }

    private void UpdateProfileName(string language)
    {
        SetLocalization();

        SetProfileName(_profilesController.Service.CurrentProfile);
    }

    private void SetLocalization()
    {
        if(string.IsNullOrEmpty(_formatLocalizationKey) == false)
        {
            _localizedFormat = LocalizationService.Instance.GetText(_formatLocalizationKey);
        }

        if(_prefix.Length > 0)
        {
            _localizedPrefix = LocalizationService.Instance.GetText(_prefix);
        }

        if(_suffix.Length > 0)
        {
            _localizedSuffix = LocalizationService.Instance.GetText(_suffix);
        }
    }

    private void OnDestroy()
    {
        _profilesController.Service.SelectedProfile -= SetProfileName;
        LocalizationService.Instance.OnLanguageChanged -= UpdateProfileName;
    }

}
