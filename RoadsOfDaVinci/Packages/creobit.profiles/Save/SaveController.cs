using Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors;
using Log = Creobit.Logger.Log;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using VContainer;
using System;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    public class SaveController : ISaveController
    {        
        private IPlayerProfilesController _playerProfilesController;
        private IPlayerPrefsSaveProvider _saveProvider;
        private ApplicationEventsInterceptor _applicationEventsInterceptor;        

        private SaveData _currentSaveData;


        public SaveService Service { get; private set; }        

        public SaveData CurrentSaveData => _currentSaveData.Clone();

        public event Action DataSaved;
        public event Action DataLoaded;

        [Inject]
        private void Construct(
            ApplicationEventsInterceptor applicationEventsInterceptor,
            IPlayerProfilesController playerProfilesController,
            IPlayerPrefsSaveProvider saveProvider)
        {
            _applicationEventsInterceptor = applicationEventsInterceptor;
            _playerProfilesController = playerProfilesController;
            _saveProvider = saveProvider;
        }

        public UniTask Load()
        {
            _applicationEventsInterceptor.Quitting += Save;
            _applicationEventsInterceptor.FocusStateChanged += GameStateChanged;
            _applicationEventsInterceptor.PauseStateChanged += GameStateChanged;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _applicationEventsInterceptor.Quitting -= Save;
            _applicationEventsInterceptor.FocusStateChanged -= GameStateChanged;
            _applicationEventsInterceptor.PauseStateChanged -= GameStateChanged;
        }

        private void GameStateChanged(bool state)
        {
            Save();
        }

        public void Save()
        {
            Service.SaveProfile(_playerProfilesController.Service.CurrentProfile);

            DataSaved?.Invoke();
        }

        public void Reset()
        {
            var currentProfile = _playerProfilesController.Service.CurrentProfile;
            
            _currentSaveData = SaveData.Create(currentProfile);
            Service = new(_currentSaveData, _saveProvider);
            Service.CreateProfileSave(currentProfile);
        }

        public void LoadData()
        {
            var currentProfile = _playerProfilesController.Service.CurrentProfile;

            try
            {
                var json = _saveProvider.TryGetValue(currentProfile.Name);
                var data = JsonConvert.DeserializeObject<SaveData>(json);

                if (data == null)
                {
                    Reset();

                    Log.Bootstrap.Info($"Initialized save data for profile {currentProfile.Name}");
                }
                else
                {
                    _currentSaveData = data;
                    Service = new(_currentSaveData, _saveProvider);
                    Log.Bootstrap.Info($"Loaded save data for profile {currentProfile.Name}");
                }

                DataLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Bootstrap.Error($"Failed to load save data for profile {currentProfile.Name}: {ex.Message}");

                Reset();
            }
        }                    
    }    
}