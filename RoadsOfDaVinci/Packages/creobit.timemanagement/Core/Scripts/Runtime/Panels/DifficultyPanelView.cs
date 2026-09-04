using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.UI;
using Cysharp.Threading.Tasks;
using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Panels
{


    
    public class DifficultyPanelView : PanelData
    {
        private const float ButtonThrottleSeconds = 0.1f;

        [SerializeField]
        [InfoBox("Buttons should be in order of increasing difficulty. (Easy - 0, Normal - 1, Hard - 2, Expert - 3, Master - 4, etc)")]
        private DifficultyButton[] _difficultyButtons;
        private IPlayerProfilesController _profilesController;
        private ISaveController _saveController;
        private readonly CompositeDisposable _disposables = new();

        private int _desiredDifficulty;
        private Button _savedButton;

        private GameMode CurrentGameMode => _saveController.CurrentSaveData.ProfileData.GameMode;

        [Inject]
        private void Construct(
            IPlayerProfilesController profilesController,
            ISaveController saveController)
        {
            _profilesController = profilesController;
            _saveController = saveController;
        }

        public override UniTask Load()
        {
            InitializeDifficultyButtons();

            _profilesController.Service.SelectedProfile += OnProfileSelected;
            _profilesController.Service.AddedProfile += OnProfileSelected;

            return base.Load();
        }

        private void InitializeDifficultyButtons()
        {
            for (var difficultyLevel = 0; difficultyLevel < _difficultyButtons.Length; difficultyLevel++)
            {
                var button = _difficultyButtons[difficultyLevel];
                if (!Enum.IsDefined(typeof(GameMode), difficultyLevel))
                    break;

                button.tooltipObject.SetActive(false);
                SetupButtonState(button.button, difficultyLevel);
                SetupButtonClickHandler(button, difficultyLevel);
            }
        }

        private void SetupButtonState(Button button, int difficultyLevel)
        {
            var currentGameMode = (int)CurrentGameMode;
            button.interactable = difficultyLevel != currentGameMode;
        }

        private void SetupButtonClickHandler(DifficultyButton button, int difficultyLevel)
        {
            button.button.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(ButtonThrottleSeconds))
                .Subscribe(_ =>
                {
                    _desiredDifficulty = difficultyLevel;
                    SaveDifficulty();
                    UpdateButtonStates(button.button);
                })
                .AddTo(_disposables);
            button.button.OnPointerEnterAsObservable().Subscribe(_ =>
            {
                button.tooltipObject?.SetActive(true);
            })
            .AddTo(_disposables);
            button.button.OnPointerExitAsObservable().Subscribe(_ =>
            {
                button.tooltipObject?.SetActive(false);
            })
            .AddTo(_disposables);
        }

        public void SaveDifficulty()
        {
            _savedButton = null;
            SaveCurrentButton();
            UpdateGameMode(_desiredDifficulty);
        }

        public void ResetButtonStates()
        {
            if (_savedButton == null) return;

            UpdateButtonStates(_savedButton);

            _savedButton = null;
        }

        private void UpdateGameMode(int difficultyLevel)
        {
            var gameMode = (GameMode)difficultyLevel;

            _profilesController.Service.CurrentProfile.GameMode = gameMode;

            _saveController.Service.SetGameMode(gameMode);
        }

        private void UpdateButtonStates(Button selectedButton)
        {
            SaveCurrentButton();

            foreach (var button in _difficultyButtons)
            {
                button.button.interactable = true;
            }

            selectedButton.interactable = false;
        }

        private void SaveCurrentButton()
        {
            if (_savedButton == null)
            {
                foreach (var button in _difficultyButtons)
                {
                    if (button.button.interactable == false)
                    {
                        _savedButton = button.button;
                    }
                }
            }
        }

        private void OnProfileSelected(PlayerProfileData profileData)
        {
            int selectedGameMode = (int)profileData.GameMode;

            UpdateGameMode(selectedGameMode);
            UpdateButtonStates(_difficultyButtons[selectedGameMode].button);
        }

        public override void Dispose()
        {
            _profilesController.Service.SelectedProfile -= OnProfileSelected;
            _profilesController.Service.AddedProfile -= OnProfileSelected;

            _disposables.Dispose();
            base.Dispose();
        }
    }

    [Serializable]
    public class DifficultyButton
    {
        public Button button;
        public GameObject tooltipObject;
    }
}