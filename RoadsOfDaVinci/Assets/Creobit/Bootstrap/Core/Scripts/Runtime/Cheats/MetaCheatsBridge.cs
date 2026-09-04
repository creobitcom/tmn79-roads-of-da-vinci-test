using System.Collections.Generic;
using System.Linq;
using _8floor.TimeManagement.Core.Scripts.Runtime.Collectibles;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics;
using _8floor.TimeManagement.Core.Scripts.Runtime.Comics.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Data;
using _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Fading;
using Creobit.Bootstrap.Core.Scripts.Runtime.Meta;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.EditionsUpgrade;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Cheats
{
    public class MetaCheatsBridge : MonoBehaviour
    {
        private const float MapOpenTimeoutSeconds = 5f;
        private const int AllLevelsUnlockedNumber = 69;

        [SerializeField]
        private LoadingBridge _loadingBridge;
        
        private IMetaController _metaController;
        private IComicsController _comicsController;
        private ISaveController _saveController;
        private IPlayerProfilesController _profilesController;
        private IFadeController _fadeController;
        private IMapController _mapController;
        private IVideoCutsceneController _videoCutsceneController;
        private ICollectiblesService _collectiblesService;
        private CollectiblesDatabaseSO _collectiblesDatabase;
        private IPlayerPrefsSaveProvider _saveProvider;

        private AllLevelsSO AllLevels => _metaController?.SceneReferences?.allLevels;

        [Inject]
        private void Construct(IMetaController metaController,
            IComicsController comicsController,
            ISaveController saveController,
            IPlayerProfilesController profilesController,
            IFadeController fadeController,
            IMapController mapController,
            IVideoCutsceneController videoCutsceneController,
            IPlayerPrefsSaveProvider saveProvider,
            ICollectiblesService collectiblesService = null,
            CollectiblesDatabaseSO collectiblesDatabase = null)
        {
            _saveProvider = saveProvider;
            _videoCutsceneController = videoCutsceneController;
            _metaController = metaController;
            _comicsController = comicsController;
            _saveController = saveController;
            _profilesController = profilesController;
            _fadeController = fadeController;
            _mapController = mapController;
            _collectiblesService = collectiblesService;
            _collectiblesDatabase = collectiblesDatabase;
        }

        public async void ResetSaves()
        {
            if (_metaController.CurrentStates.Peek() is not MapState)
            {
                _metaController.ChangeState(MetaStates.Map);
                return;
            }
            
            _saveController.Reset();
            _profilesController.Service.ResetProfiles();

            _saveProvider.DeleteAll();

            await _fadeController.FadeIn();
            _loadingBridge.LoadMenu();
        }
        
        public void UnlockAllLevels()
        {
            UnlockLevels(AllLevelsUnlockedNumber);
        }

        private void UnlockLevels(int lastUnlockedLevel)
        {
            var maxCampaignLevel = GetLevelsCount();

            if (maxCampaignLevel > 0)
            {
                lastUnlockedLevel = Mathf.Min(lastUnlockedLevel, maxCampaignLevel);
            }

            _saveController.Service.TrySaveLastUnlockedLevel(lastUnlockedLevel);

            for (var i = 1; i <= lastUnlockedLevel; i++)
            {
                PlayerPrefs.SetInt(UnlockLevelItem.UnlockedLevelKey + i, 1);
            }

            if (PlayerPrefs.GetInt(UnlockLevelManager.LastUnlockedLevelIndexKey) < lastUnlockedLevel)
            {
                PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, lastUnlockedLevel);
            }

            PlayerPrefs.Save();

            _saveController.Save();

            if (_metaController.CurrentStates.Peek() is MapState)
            {
                _metaController.RefreshMapAfterUnlockCheat();
            }
            else
            {
                _metaController.ChangeState(MetaStates.Map);
            }
        }

        public void ResetF2PPurchases()
        {
            PlayerPrefs.DeleteKey(UnlockLevelManager.AllLevelsBuyKey);
            PlayerPrefs.DeleteKey("FirstWindowNumber");
            PlayerPrefs.DeleteKey("UnlockLevelWindow");

            var levelsCount = Mathf.Max(GetLevelsCount(), 100);

            for (var i = 1; i <= levelsCount; i++)
            {
                PlayerPrefs.DeleteKey(UnlockLevelItem.UnlockedLevelKey + i);
                PlayerPrefs.DeleteKey("Timer" + i);
                PlayerPrefs.DeleteKey("StartTime" + i);
            }

            PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, Mathf.Max(1, GetLastUnlockedLevel()));
            PlayerPrefs.Save();

            if (_metaController.CurrentStates.Peek() is MapState)
            {
                _metaController.RefreshMapAfterUnlockCheat(false);
            }
        }
        
        public int GetLevelsCount()
        {
            var allLevels = AllLevels;

            return allLevels != null ? allLevels.MaxCampaignLevel : 0;
        }

        public int GetLastUnlockedLevel()
        {
            return _mapController is MapController mapController ? mapController.LastUnlockedLevel : 0;
        }

        public async void GoToLevel(int levelNum)
        {
            var allLevels = AllLevels;

            if (allLevels != null && !allLevels.IsLevelAvailable(levelNum))
            {
                Debug.LogWarning(
                    $"[MetaCheatsBridge] Level {levelNum} is not available in this edition (max {allLevels.MaxCampaignLevel}).");
                return;
            }

            if (!await TryOpenMap())
            {
                Debug.LogWarning("[MetaCheatsBridge] The map did not open, so the level was not started.");
                return;
            }

            if (levelNum > GetLastUnlockedLevel())
            {
                UnlockLevels(levelNum);
            }

            MapSpotView spot = _mapController.GetMapSpot(levelNum);

            if (spot == null)
            {
                Debug.LogWarning($"[MetaCheatsBridge] There is no map spot for level {levelNum}.");
                return;
            }

            _mapController.SelectMapSpot(spot);

            if (_mapController.CurrentLevel != levelNum)
            {
                Debug.LogWarning($"[MetaCheatsBridge] Level {levelNum} could not be selected on the map.");
                return;
            }

            _metaController.ChangeState(MetaStates.Gameplay);
        }

        private async UniTask<bool> TryOpenMap()
        {
            if (_metaController.CurrentStates.Peek() is MapState)
            {
                return true;
            }

            var mapOpened = false;

            void MapOpenedHandler() => mapOpened = true;

            // The map is only usable once MapState.StartState has run, and that happens
            // asynchronously, after the outgoing state has played its hide animations.
            _mapController.OnMapOpened += MapOpenedHandler;

            try
            {
                _metaController.ChangeState(MetaStates.Map);

                var deadline = Time.unscaledTime + MapOpenTimeoutSeconds;

                while (!mapOpened && Time.unscaledTime < deadline)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
            }
            finally
            {
                _mapController.OnMapOpened -= MapOpenedHandler;
            }

            return mapOpened;
        }

        public int GetAvailableComicsCount()
        {
            return GetAvailableComics().Count;
        }

        private List<ComicsData> GetAvailableComics()
        {
            var result = new List<ComicsData>();
            var comicsData = _comicsController?.ComicsData;

            if (comicsData == null)
            {
                return result;
            }

            var allLevels = AllLevels;

            foreach (var comics in comicsData)
            {
                if (comics == null)
                {
                    continue;
                }

                if (allLevels != null && comics.LevelToShow > 0 && !allLevels.IsLevelAvailable(comics.LevelToShow))
                {
                    continue;
                }

                result.Add(comics);
            }

            return result;
        }

        public void ShowComicsByIndex(int index)
        {
            var comicsData = GetAvailableComics();

            if (index < 1 || index > comicsData.Count)
            {
                Debug.LogWarning(
                    $"[MetaCheatsBridge] Comic index {index} is out of range (1..{comicsData.Count}).");
                return;
            }

            ShowComics(comicsData[index - 1].ComicsName);
        }
        
        public void ShowComics(string name)
        {
            var comics = _comicsController.ComicsData.FirstOrDefault(comicsData => comicsData.ComicsName == name);
            if (comics == null)
            {
                Debug.LogWarning($"[MetaCheatsBridge] Comic '{name}' was not found in the current meta scene.");
                return;
            }
            
            _saveController.Service.DeleteComics(name);
            _metaController.ForceShowComics(comics);
        }

        public int GetVideoCutscenesCount()
        {
            return GetAvailableVideoCutscenes().Count;
        }

        private List<VideoCutsceneSO> GetAvailableVideoCutscenes()
        {
            var result = new List<VideoCutsceneSO>();
            var cutscenes = _videoCutsceneController?.Cutscenes;

            if (cutscenes == null)
            {
                return result;
            }

            var allLevels = AllLevels;

            foreach (var cutscene in cutscenes)
            {
                if (cutscene == null)
                {
                    continue;
                }

                if (allLevels != null && cutscene.Level > 0 && !allLevels.IsLevelAvailable(cutscene.Level))
                {
                    continue;
                }

                result.Add(cutscene);
            }

            return result;
        }

        /// <summary>Проиграть ролик по номеру из библиотеки (1..N), не создавая новый профиль.</summary>
        public void PlayVideoCutsceneByIndex(int index)
        {
            var cutscenes = GetAvailableVideoCutscenes();

            if (index < 1 || index > cutscenes.Count)
            {
                Debug.LogWarning(
                    $"[MetaCheatsBridge] Cutscene index {index} is out of range (1..{cutscenes.Count}).");
                return;
            }

            var cutscene = cutscenes[index - 1];

            _saveController.Service.DeleteComics(cutscene.SaveKey);
            _metaController.ShowVideoCutscene(cutscene);
        }

        /// <summary>Снять отметки "просмотрено" со всех роликов текущего профиля.</summary>
        public void ResetVideoCutscenes()
        {
            var cutscenes = _videoCutsceneController?.Cutscenes;

            if (cutscenes == null)
            {
                return;
            }

            foreach (var cutscene in cutscenes)
            {
                if (cutscene != null)
                {
                    _saveController.Service.DeleteComics(cutscene.SaveKey);
                }
            }

            _saveController.Save();

            Debug.Log($"[MetaCheatsBridge] Cutscene watch flags reset ({cutscenes.Count}).");
        }

        public void UnlockAllArtifacts()
        {
            UnlockAllCollectiblesByType(CollectibleType.Artifact);
        }

        public void UnlockAllTrophies()
        {
            UnlockAllCollectiblesByType(CollectibleType.Trophy);
        }

        private void UnlockAllCollectiblesByType(CollectibleType type)
        {
            if (_collectiblesDatabase == null || _collectiblesService == null)
            {
                Debug.LogWarning("[MetaCheatsBridge] CollectiblesDatabase or CollectiblesService was not found.");
                return;
            }

            var allLevels = AllLevels;
            var groups = _collectiblesDatabase.GetGroups(type);
            foreach (var group in groups)
            {
                if (group == null || group.Items == null) continue;
                foreach (var item in group.Items)
                {
                    if (item == null) continue;
                    if (allLevels != null && !allLevels.IsCollectibleAvailable(item)) continue;
                    int cur = _collectiblesService.GetProgress(item.Id);
                    if (cur < item.MaxParts)
                    {
                        _collectiblesService.AddProgress(item, item.MaxParts - cur);
                    }
                }
            }

            _saveController.Save();
            Debug.Log($"[MetaCheatsBridge] All {type}s unlocked!");
        }
    }
}
