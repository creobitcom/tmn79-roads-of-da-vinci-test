using System;
using System.Collections.Generic;
using System.Globalization;
using Creobit.Audio;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility;
using Creobit.Bootstrap.Core.Scripts.Runtime.Utility.Scene;
using Creobit.Loading;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using R3;
using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    public class GameSettingsService : ILoadUnit, IDisposable
    {
        #region Constants
        private const int DefaultWindowWidth = 1366;
        private const int DefaultWindowHeight = 768;
        #endregion

        #region Private Fields
        private readonly Dictionary<string, Action<object>> _settingsHandlers;
        private CompositeDisposable _settingsPropertyListeners;
        private BootstrapPrefabReferences _prefabReferences;
        private IAudioService _audioService;
        private IPlayerPrefsSaveProvider _saveProvider;
        private ISaveController _saveController;
        private IPlayerProfilesController _playerProfilesController;
        #endregion

        #region Public Properties
        public GameSettings Settings { get; private set; }
#endregion

        public GameSettingsService()
        {
            _settingsHandlers = new Dictionary<string, Action<object>>
            {
                { nameof(GameSettings.MusicVolume), value => OnMusicVolumeChanged((float)value) },
                { nameof(GameSettings.SfxVolume), value => OnSfxVolumeChanged((float)value) },
                { nameof(GameSettings.IsFullScreen), value => OnIsFullScreenValueChanged((bool)value) },
                { nameof(GameSettings.IsSystemCursor), value => OnIsSystemCursorValueChanged((bool)value) }
            };
        }

        [Inject]
        public void Construct(BootstrapPrefabReferences prefabReferences, IAudioService audioController,
            IPlayerPrefsSaveProvider saveProvider,
            ISaveController saveController,
            IPlayerProfilesController playerProfilesController)
        {
            _prefabReferences = prefabReferences;
            _audioService = audioController;
            _saveProvider = saveProvider;
            _saveController = saveController;
            _playerProfilesController = playerProfilesController;
        }

        public UniTask Load()
        {
            InitializeSettings();
            SubscribeToSettingsChanges();

            _playerProfilesController.Service.AddedProfile += OnProfileSelectedHandler;
            _playerProfilesController.Service.SelectedProfile += OnProfileSelectedHandler;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_playerProfilesController?.Service != null)
            {
                _playerProfilesController.Service.AddedProfile -= OnProfileSelectedHandler;
                _playerProfilesController.Service.SelectedProfile -= OnProfileSelectedHandler;
            }

            _settingsPropertyListeners?.Dispose();
        }

        #region Private Methods
        private void InitializeSettings()
        {
            var settingsSave = _saveController.CurrentSaveData.Settings;
            var savedSettings = new GameSettings(settingsSave.MusicVolume, settingsSave.SfxVolume,
                settingsSave.IsFullScreen, settingsSave.IsSystemCursor, settingsSave.Language);
            Log.Bootstrap.Info("SAVED SETTINGS  = " + (savedSettings == null));
            if (savedSettings.IsInitialized())
            {
                Settings = _prefabReferences.DefaultSettings;
                _saveController.Service.SetFullscreenState(Settings.IsFullScreen);
                _saveController.Service.SetSystemCursorState(Settings.IsSystemCursor);
                _saveController.Service.SetMusicVolume(Settings.MusicVolume);
                _saveController.Service.SetSfxVolume(Settings.SfxVolume);
            }
            else
            {
                Settings = savedSettings;                
            }            

            _settingsPropertyListeners?.Dispose();
            SubscribeToSettingsChanges();
        }

        private void OnProfileSelectedHandler(PlayerProfileData profileData)
        {
            InitializeSettings();

            OnMusicVolumeChanged(Settings.MusicVolume);
            OnSfxVolumeChanged(Settings.SfxVolume);
            OnIsFullScreenValueChanged(Settings.IsFullScreen);
            OnIsSystemCursorValueChanged(Settings.IsSystemCursor);
        }

        private void SubscribeToSettingsChanges()
        {
            _settingsPropertyListeners = new CompositeDisposable();

            foreach (var handler in _settingsHandlers)
            {
                Observable
                    .EveryValueChanged(Settings, s => s.GetType().GetProperty(handler.Key)?.GetValue(s))
                    .Subscribe(handler.Value)
                    .AddTo(_settingsPropertyListeners);
            }
        }


        private void Save()
        {
            _saveProvider.Save(RuntimeConstants.GameSettingsPrefs.Settings, 
                JsonConvert.SerializeObject(Settings).ToString(CultureInfo.InvariantCulture));
        }

        private void OnMusicVolumeChanged(float value)
        {
            _audioService.SetMusicVolume(value);

            _saveController.Service.SetMusicVolume(value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            _audioService.SetSfxVolume(value);

            _saveController.Service.SetSfxVolume(value);
        }

        private void OnIsFullScreenValueChanged(bool value)
        {
            Screen.SetResolution(
                value ? Screen.currentResolution.width : DefaultWindowWidth,
                value ? Screen.currentResolution.height : DefaultWindowHeight,
                value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);

            _saveController.Service.SetFullscreenState(value);
        }

        private void OnIsSystemCursorValueChanged(bool value)
        {
            Cursor.SetCursor(value ? null : _prefabReferences.Cursor, new Vector2(5f, 0f), CursorMode.Auto);

            _saveController.Service.SetSystemCursorState(value);
        }
#endregion
    }
}