using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Cheats;
using UnityEngine;
using UnityEngine.SceneManagement;

#if TOYMAN
using TimeManagerEngine;
#elif GAME_ON
using Profile;
using Strategop.Common.Events;
using Strategop.Common.Profiles;
using Strategop.Game.Level;
using Strategop.Game.Resources;
#endif

namespace Creobit.EditionsUpgrade
{
    public class ProjectCheats
    {
        private ProjectScenes _projectScenes;

        public ProjectCheats()
        {
            _projectScenes = new ProjectScenes();
        }

        public ICheat[] GetCheats()
        {
            List<ICheat> result = new List<ICheat>();

#if TOYMAN
            result.AddRange(GetToymanCheats());
#elif GAME_ON
            result.AddRange(GetGameOnCheats());
#elif ARGUNOV
            result.AddRange(GetArgunovCheats());
#elif CREOBIT
            result.AddRange(GetCreobitCheats());
#endif

            return result.ToArray();
        }

#if CREOBIT
        private ICheat[] GetCreobitCheats()
        {
            List<ICheat> result = new()
            {
                new ScenesCheat("Reset saves (open map)", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().ResetSaves();
                }),
                
                new ScenesCheat("Unlock levels", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().UnlockAllLevels();
                }),

                new ScenesCheat("Reset F2P purchase", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().ResetF2PPurchases();
                }),

                new ScenesCheat("Go to level (pick a number)", new string[] { _projectScenes.Meta }, ShowLevelPicker),

                new ScenesCheat("Play meta comic (pick a number)", new string[] { _projectScenes.Meta }, ShowComicsPicker),

                new ScenesCheat("Play video cutscene (pick a number)", new string[] { _projectScenes.Meta },
                    ShowVideoCutscenePicker),

                new ScenesCheat("Reset video cutscenes", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().ResetVideoCutscenes();
                }),

                new ScenesCheat("Unlock all artifacts", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().UnlockAllArtifacts();
                }),

                new ScenesCheat("Unlock all trophies", new string[] { _projectScenes.Meta }, () =>
                {
                    Object.FindFirstObjectByType<MetaCheatsBridge>().UnlockAllTrophies();
                }),

                new ScenesCheat("Walk through objects", new []{_projectScenes.Gameplay}, GameplayCheats.WalkThroughObjects),

                new ScenesCheat("Finish level (win)", new []{_projectScenes.Gameplay}, GameplayCheats.FinishLevel),

                new ScenesCheat("Complete all tasks", new []{_projectScenes.Gameplay}, GameplayCheats.CompleteAllTasks),

                new ScenesCheat("Level pass timer: ON", new []{_projectScenes.Meta, _projectScenes.Gameplay},
                    () => GameplayCheats.SetLevelPassTimerEnabled(true)),

                new ScenesCheat("Level pass timer: OFF", new []{_projectScenes.Meta, _projectScenes.Gameplay},
                    () => GameplayCheats.SetLevelPassTimerEnabled(false)),

                new ScenesCheat("Ambience (light presets)", new []{_projectScenes.Gameplay},
                    ShowAmbiencePicker),

                new ScenesCheat("Play cutscene (comics)", new []{_projectScenes.Gameplay}, PlayCutscene),

                new ScenesCheat("+300 resources", new []{_projectScenes.Gameplay}, () =>
                {
                    GameplayCheats.GiveResources(300);
                }),

                new ScenesCheat("Units speed x2 (max x8)", new []{_projectScenes.Gameplay}, GameplayCheats.CycleUnitsSpeed),

                new ScenesCheat("Reset units speed", new []{_projectScenes.Gameplay}, GameplayCheats.ResetUnitsSpeed),

                new ScenesCheat("Interaction speed x2 (max x8)", new []{_projectScenes.Gameplay}, GameplayCheats.CycleInteractionSpeed),

                new ScenesCheat("Reset interaction speed", new []{_projectScenes.Gameplay}, GameplayCheats.ResetInteractionSpeed),

                new ScenesCheat("Win with 0 stars", new []{_projectScenes.Gameplay},
                    () => GameplayCheats.FinishLevelWithStars(0)),

                new ScenesCheat("Win with 1 star", new []{_projectScenes.Gameplay},
                    () => GameplayCheats.FinishLevelWithStars(1)),

                new ScenesCheat("Win with 2 stars", new []{_projectScenes.Gameplay},
                    () => GameplayCheats.FinishLevelWithStars(2)),

                new ScenesCheat("Win with 3 stars", new []{_projectScenes.Gameplay},
                    () => GameplayCheats.FinishLevelWithStars(3))
            };

            return result.ToArray();
        }

        private void PlayCutscene()
        {
            // Прячем панель читов, иначе комикс окажется под ней.
            CheatsPanel cheatsPanel = Object.FindFirstObjectByType<CheatsPanel>();

            if (cheatsPanel != null)
            {
                cheatsPanel.SetVisible(false);
            }

            GameplayCheats.PlayCutscene();
        }

        private void ShowAmbiencePicker()
        {
            CheatsCanvas cheatsCanvas = Object.FindFirstObjectByType<CheatsCanvas>();

            if (cheatsCanvas == null)
            {
                return;
            }

            CheatsPanel cheatsPanel = Object.FindFirstObjectByType<CheatsPanel>();

            if (cheatsPanel != null)
            {
                cheatsPanel.SetVisible(false);
            }

            AmbiencePickerView.Show(cheatsCanvas.transform);
        }

        private void ShowComicsPicker()
        {
            MetaCheatsBridge cheatsBridge = Object.FindFirstObjectByType<MetaCheatsBridge>();
            CheatsCanvas cheatsCanvas = Object.FindFirstObjectByType<CheatsCanvas>();

            if (cheatsBridge == null || cheatsCanvas == null)
            {
                return;
            }

            int count = cheatsBridge.GetAvailableComicsCount();

            if (count == 0)
            {
                Debug.LogWarning("[Cheats] No comics found in Meta scene.");
                return;
            }

            LevelPickerView.Show(cheatsCanvas.transform, count, count, index =>
            {
                CheatsPanel cheatsPanel = Object.FindFirstObjectByType<CheatsPanel>();

                if (cheatsPanel != null)
                {
                    cheatsPanel.SetVisible(false);
                }

                cheatsBridge.ShowComicsByIndex(index);
            });
        }

        private void ShowVideoCutscenePicker()
        {
            MetaCheatsBridge cheatsBridge = Object.FindFirstObjectByType<MetaCheatsBridge>();
            CheatsCanvas cheatsCanvas = Object.FindFirstObjectByType<CheatsCanvas>();

            if (cheatsBridge == null || cheatsCanvas == null)
            {
                return;
            }

            int count = cheatsBridge.GetVideoCutscenesCount();

            if (count == 0)
            {
                Debug.LogWarning("[Cheats] В библиотеке нет ни одной видео-катсцены.");
                return;
            }

            // Номера соответствуют порядку катсцен в CutsceneLibrary.
            LevelPickerView.Show(cheatsCanvas.transform, count, count, index =>
            {
                CheatsPanel cheatsPanel = Object.FindFirstObjectByType<CheatsPanel>();

                if (cheatsPanel != null)
                {
                    cheatsPanel.SetVisible(false);
                }

                cheatsBridge.PlayVideoCutsceneByIndex(index);
            });
        }

        private void ShowLevelPicker()
        {
            MetaCheatsBridge cheatsBridge = Object.FindFirstObjectByType<MetaCheatsBridge>();
            CheatsCanvas cheatsCanvas = Object.FindFirstObjectByType<CheatsCanvas>();

            if (cheatsBridge == null || cheatsCanvas == null)
            {
                return;
            }

            LevelPickerView.Show(cheatsCanvas.transform, cheatsBridge.GetLevelsCount(),
                cheatsBridge.GetLastUnlockedLevel(), levelNum =>
                {
                    CheatsPanel cheatsPanel = Object.FindFirstObjectByType<CheatsPanel>();

                    if (cheatsPanel != null)
                    {
                        cheatsPanel.SetVisible(false);
                    }

                    cheatsBridge.GoToLevel(levelNum);
                });
        }
#endif

#if TOYMAN
        private ICheat[] GetToymanCheats()
        {
            ICheat[] result = new ICheat[]
            {

               new ScenesCheat("Delete profile data", new string[] { _projectScenes.Menu, _projectScenes.LevelsSelector }, () =>
               {
                   if (string.IsNullOrEmpty(Player.I.SelectedId))
                   {
                       return;
                   }

                   string profileName = Player.I.SelectedId;

                   Player.I.DeletePlayer();

                   Player.I.AddPlayer(profileName);
               }),

                new ScenesCheat("Unlock all lvls", new string[] { _projectScenes.LevelsSelector }, () =>
                {
                     Player.Instance.MaxAvailableLevel = Player.MAX_LEVEL;

                    for (int i = 0; i < Player.Instance.levelStars.Length; ++i)
                    {
                        Player.Instance.levelStars[i] = 3;

                        Player.I.levelUnlock[i] = 1;
                    }

                    Player.I.Save();

                    PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, Player.MAX_LEVEL);

                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }),

                new ScenesCheat("Unlock +1 lvl", new string[] { _projectScenes.LevelsSelector }, () =>
                {
                    int newLvlIndex = Player.Instance.MaxAvailableLevel + 1;

                    Player.Instance.MaxAvailableLevel = Player.Instance.MaxAvailableLevel + 1;
                    Player.Instance.levelStars[newLvlIndex] = 3;
                    Player.I.levelUnlock[newLvlIndex] = 1;

                    Player.I.Save();

                    PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, newLvlIndex);

                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }),

                new ScenesCheat("Complete lvl", _projectScenes.Levels, () => TaskManager.I.Edt_ForceWin()),

                new ScenesCheat("Get resources", _projectScenes.Levels, () =>
                {
                    ResourcesAmount resourcesToAdd = new ResourcesAmount()
                    {
                        food = 10,
                        gem = 10,
                        stone = 10,
                        wood = 10
                    };

                    Player.I.AddResources(resourcesToAdd);
                }),

                new ScenesCheat("Reset boosts", _projectScenes.Levels, () =>
                {
                    foreach (BoostsManager.Boost curBoost in BoostsManager.I.boosts.Values)
                    {
                        curBoost.remainingWorkTime = 0;
                        curBoost.RemainingCooldownTime = 1;
                    }
                }),
            };

            return result;
        }
#endif

#if ARGUNOV
        private ICheat[] GetArgunovCheats()
        {
            ICheat[] result = new ICheat[]
            {
                new ScenesCheat("Delete profile data", new string[] { _projectScenes.Menu, _projectScenes.LevelsSelector }, () =>
               {
                   if (string.IsNullOrEmpty(PlayerProfileManager.I.CurrentProfileName))
                   {
                       return;
                   }

                   string profileName = PlayerProfileManager.I.CurrentProfileName;

                   PlayerProfileManager.I.DeleteProfile(profileName);
                   PlayerProfileManager.I.CreateProfile(profileName);
                   PlayerProfileManager.I.SetCurrentProfile(profileName);
               }),

                new ScenesCheat("Unlock all lvls", new string[] { _projectScenes.LevelsSelector }, () =>
                {
                    int countLevels = UserData.COUNT_LEVELS_IN_GAME;

                    if (ProjectSettings.I.IsBFG_Survey) {
                        countLevels = UserData.SURVEY_MAX_LEVELS;
                    }

                    int comicsNumber = 0;

                    for (int i = 1; i <= countLevels; i++) {
                        comicsNumber = TransitionManager.I.GetComicsNumber(i);

                        if (comicsNumber > 0) {
                            UserData.I.SetComicsStatus(comicsNumber, true);
                        }

                        Level level = LevelParser.I.GetLevel(i);
                        if (level != null) {
                            UserData.I.SaveLevelDump(i, 0, true, 3, 0);
                        } else break;
                    }
                    TransitionManager.I.RestartScene();
                }),

                new ScenesCheat("Next lvl", _projectScenes.Levels, () =>
                {
                    Level level = LevelParser.I.GetLevel(TransitionManager.I.TargetLevel);
                    if (level != null) {
                        UserData.I.SaveLevelDump(TransitionManager.I.TargetLevel, 0, true, 3, 0);
                        TransitionManager.I.LoadLevel(TransitionManager.I.TargetLevel + 1);
                    }
                }),

                new ScenesCheat("Previous lvl", _projectScenes.Levels, () =>
                {
                    Level level = LevelParser.I.GetLevel(TransitionManager.I.TargetLevel);
                    if (level != null) {
                        UserData.I.SaveLevelDump(TransitionManager.I.TargetLevel, 0, true, 3, 0);
                        TransitionManager.I.LoadLevel(TransitionManager.I.TargetLevel - 1);
                    }
                }),
                
                new ScenesCheat("Complete lvl", _projectScenes.Levels, () => GameManager.I.DoPassLevel(3)),

                new ScenesCheat("Add resources", _projectScenes.Levels, () =>
                {
                    GameManager.I.AddResources(new List<Resource>() {
                        new Resource(Resource.Types.Food, 10),
                        new Resource (Resource.Types.Stone, 10),
                        new Resource (Resource.Types.Wood, 10),
                        new Resource (Resource.Types.Gold, 10)
                    });
                }),

                new ScenesCheat("Reset boosts", _projectScenes.Levels, () =>
                {
                    GameManager.I.FillBonuses();
                }),
            };

            return result;
        }
#endif

#if GAME_ON
        private ICheat[] GetGameOnCheats()
        {
            ICheat[] result = new ICheat[]
            {
               new ScenesCheat("Delete profile data", new string[] { _projectScenes.Menu, _projectScenes.LevelsSelector }, () =>
               {
                   string currentProfileName = ProfileManager.Instance.ActiveProfileName;

                   if (string.IsNullOrEmpty(currentProfileName))
                   {
                       return;
                   }

                   string newProfileName = $"Player{Random.Range(1, 100)}";

                   ProfileManager.Instance.SetActiveProfile<EmergencyCrewPlayerProfile>(newProfileName);

                   ProfileManager.Instance.DeleteProfile<EmergencyCrewPlayerProfile>(currentProfileName);
               }),

                new ScenesCheat("Unlock all lvls", new string[] { _projectScenes.LevelsSelector }, () =>
                {
                    string[] levelsScenesNames = _projectScenes.Levels;

                    for(int i = 0; i < levelsScenesNames.Length; i++)
                    {
                        LevelResult levelResult = ProfileManager.Instance.GetActiveProfile<EmergencyCrewPlayerProfile>()
                                                                         .GetLevelResult(levelsScenesNames[i]);

                        if (levelResult.Complete)
                        {
                            continue;
                        }

                        levelResult.Complete = true;
                        levelResult.StarsCount = 3;

                        PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, i);
                    }

                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                }),

                new ScenesCheat("Unlock +1 lvl", new string[] { _projectScenes.LevelsSelector }, () =>
                {
                    string[] levelsScenesNames = _projectScenes.Levels;

                    for(int i = 0; i < levelsScenesNames.Length; i++)
                    {
                        LevelResult levelResult = ProfileManager.Instance.GetActiveProfile<EmergencyCrewPlayerProfile>()
                                                                         .GetLevelResult(levelsScenesNames[i]);

                        if (levelResult.Complete)
                        {
                            continue;
                        }

                        levelResult.Complete = true;
                        levelResult.StarsCount = 3;

                        PlayerPrefs.SetInt(UnlockLevelManager.LastUnlockedLevelIndexKey, i);

                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

                        return;
                    }
                }),

                new ScenesCheat("Complete lvl", _projectScenes.Levels, () => 
                {
                    EventManager.Instance.DispatchEvent(new LevelEvent(LevelEventType.LevelComplete));
                }),

                new ScenesCheat("Get resources", _projectScenes.Levels, () =>
                {
                   List<LimitedResourceValue> limitedResourceValues =  Strategop.Game.Level.Level.Instance.OnLevelResources;

                    for(int i = 0; i < limitedResourceValues.Count; i++)
                    {
                        LimitedResourceValue resource = limitedResourceValues[i];

                        resource.Count += 10;

                        EventManager.Instance.DispatchEvent(new ResourceValueEvent(ResourceValueEventType.ResourceValueChanged, new ResourceValue() { Resource = resource.Resource, Count = resource.Count }));
                    }
                }),
            };

            return result;
        }
#endif
    }
}
