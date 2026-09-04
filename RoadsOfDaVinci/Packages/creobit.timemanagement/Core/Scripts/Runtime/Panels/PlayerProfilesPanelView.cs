using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Panels;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Localization;
using Creobit.UI;
using Creobit.UI.Utility;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Views
{
    public class PlayerProfilesPanelView : PanelData
    {
        [SerializeField] private Button _addProfileButton;
        [SerializeField] private Button _removeProfileButton;

        [SerializeField] private PlayerProfileView _viewPrefab;
        [SerializeField] private RectTransform _viewsParent;

        [SerializeField] private PanelReference _alertPanel;
        [SerializeField] private PanelReference _confirmationPanel;
        [SerializeField] private PanelReference _createProfilePanel;

        private PlayerProfileView _selectedView;

        private Dictionary<string, PlayerProfileView> _views = new();

        private IUIController _uiController;

        private PlayerProfilesService _profilesService;
        
#if ALL_IN_ONE
        private GameSwitcher _gameSwitcher;
#endif
        
        [Inject]
        private void Construct(IUIController uiController, IPlayerProfilesController playerProfilesController
#if ALL_IN_ONE
            , GameSwitcher gameSwitcher
#endif
            )
        {
            _uiController = uiController;
            _profilesService = playerProfilesController.Service;
#if ALL_IN_ONE
            _gameSwitcher = gameSwitcher;
#endif
        }

        public override UniTask Load()
        {
            Observable
                .EveryValueChanged(gameObject, x => x.activeSelf) // detect if panel open or close
                .Skip(1) // skip initialization
                .Subscribe(OnPanelActiveStateChanged)
                .AddTo(this);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            _addProfileButton.onClick.RemoveListener(OnAddProfileButtonClicked);
            _removeProfileButton.onClick.RemoveListener(OnRemoveProfileButtonClicked);

            _profilesService.AddedProfile -= OnProfileAdded;
            _profilesService.SelectedProfile -= OnProfileSelected;
            _profilesService.RemovedProfile -= OnProfileRemoved;
        }

        private void OnPanelActiveStateChanged(bool isActive)
        {
            if (!isActive)
            {
                _addProfileButton.onClick.RemoveListener(OnAddProfileButtonClicked);
                _removeProfileButton.onClick.RemoveListener(OnRemoveProfileButtonClicked);

                _profilesService.AddedProfile -= OnProfileAdded;
                _profilesService.SelectedProfile -= OnProfileSelected;
                _profilesService.RemovedProfile -= OnProfileRemoved;

                DestroyViews();

                return;
            }

            UpdateButtonsInteractable();

            CreateViews();

            if (_views.Count > 0)
            {
                SelectView(_views[_profilesService.SelectedProfileName]);
            }

            _addProfileButton.onClick.AddListener(OnAddProfileButtonClicked);
            _removeProfileButton.onClick.AddListener(OnRemoveProfileButtonClicked);

            _profilesService.AddedProfile += OnProfileAdded;
            _profilesService.SelectedProfile += OnProfileSelected;
            _profilesService.RemovedProfile += OnProfileRemoved;
        }

        private void CreateViews()
        {
            foreach (var profile in _profilesService.Profiles)
            {
                CreateView(profile);
            }
        }

        private void CreateView(PlayerProfileData profile)
        {
            var view = Instantiate(_viewPrefab, _viewsParent);

            view.Initialize(profile, OnViewSelected);

            _views.Add(profile.Name, view);
        }

        private void DestroyViews()
        {
            foreach (var view in _views.Values)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
        }

        private void DestroyView(string profileName)
        {
            Destroy(_views[profileName].gameObject);

            _views.Remove(profileName);
        }

        private void SelectView(PlayerProfileView view)
        {
            if (_selectedView != null)
            {
                _selectedView.SetSelectedState(false);
            }

            _selectedView = view;

            _selectedView.SetSelectedState(true);
        }

        private void UpdateButtonsInteractable()
        {
            _addProfileButton.interactable = !_profilesService.IsMaxProfilesReached;
            _removeProfileButton.interactable = !_profilesService.IsProfilesCountLow;
        }

        private async UniTaskVoid ShowErrorAlert(string message)
        {
            (await _uiController.ShowPanel<AlertPanelView>(_alertPanel, null)).SetInputData(LocalizationService.Instance.GetText("profiles_txt_header_error"), message,
                       ok: () =>
                       {
                           _uiController.HidePanel(_alertPanel, null);
                       });
        }

        private async UniTaskVoid ShowDeleteProfileConfirmationPanel()
        {
            (await _uiController.ShowPanel<ConfirmationPanelView>(_confirmationPanel, null))
                .SetInputData(LocalizationService.Instance.GetText("profiles_txt_header_delete_player"), 
                    string.Format(LocalizationService.Instance.GetText("profiles_dlg_delete_confirm"), 
                        _profilesService.CurrentProfile.Name),
            yes: () =>
            {
                _profilesService.RemoveSelectedProfile();
                _uiController.HidePanel(_confirmationPanel, null);
            },
            no: () =>
            {
                _uiController.HidePanel(_confirmationPanel, null);
            });
        }

        private async UniTaskVoid ShowCreateProfilePanel()
        {
            (await _uiController.ShowPanel<CreatePlayerProfilePanelView>(_createProfilePanel, null)).SetInputData(
                ok: (profileName) =>
                {
                    var operationResult = _profilesService.AddProfile(profileName);

                    if (!operationResult.IsSuccess)
                    {
                        ShowErrorAlert(operationResult.ErrorMessage).Forget();

                        return;
                    }

                    _uiController.HidePanel(_createProfilePanel, null);
                },
                cancel: () =>
                {
                    _uiController.HidePanel(_createProfilePanel, null);
                }
            );
        }

        private void OnAddProfileButtonClicked() => ShowCreateProfilePanel().Forget();

        private void OnRemoveProfileButtonClicked() => ShowDeleteProfileConfirmationPanel().Forget();

        private void OnProfileAdded(PlayerProfileData profile)
        {
            UpdateButtonsInteractable();

            CreateView(profile);
        }

        private void OnProfileSelected(PlayerProfileData profile)
        {
            SelectView(_views[profile.Name]);
        }

        private void OnProfileRemoved(PlayerProfileData profile)
        {
            UpdateButtonsInteractable();

            DestroyView(profile.Name);
        }

        private void OnViewSelected(PlayerProfileView view)
        {
            _profilesService.SelectProfile(view.Data.Name);
        }
    }
}