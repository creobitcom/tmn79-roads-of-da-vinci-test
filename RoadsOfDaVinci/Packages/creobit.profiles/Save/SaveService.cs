using System;
using System.Collections.Generic;
using System.Linq;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Localization;
using Creobit.Logger;
using Newtonsoft.Json;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    public class SaveService
    {
        private readonly ISaveProvider _saveProvider;

        private readonly SaveData _currentSaveData;

        public SaveService(SaveData saveData, ISaveProvider saveProvider)
        {
            _currentSaveData = saveData;
            _saveProvider = saveProvider;
            LocalizationService.Instance.OnLanguageChanged += SaveLocalization;
        }

        public void SaveProfile(PlayerProfileData profileData)
        {
            if (profileData == null)
            {
                Log.Bootstrap.Error("Profile data cannot be null.");
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(_currentSaveData, Formatting.Indented);
                _saveProvider.Save(profileData.Name, json);

                Log.Bootstrap.Info($"Saved profile data for {profileData.Name}: {json}");
            }
            catch (Exception ex)
            {
                Log.Bootstrap.Error($"Failed to save profile data: {ex.Message}");
            }
        }

        public void CreateProfileSave(PlayerProfileData profileData)
        {
            try
            {
                var saveData = SaveData.Create(profileData);
                var json = JsonConvert.SerializeObject(saveData, Formatting.Indented);
                _saveProvider.Save(profileData.Name, json);
                LocalizationService.Instance.LoadAndSetLanguage(saveData.Settings.Language);

                Log.Bootstrap.Info($"Created profile save: {json}");
            }
            catch (Exception ex)
            {
                Log.Bootstrap.Error($"Failed to create profile save: {ex.Message}");
                return;
            }
        }

        public void TrySaveLastUnlockedLevel(int level)
        {
            if (level < 1)
            {
                Log.Gameplay.Error("Level must be positive to save.");
                return;
            }

            if (_currentSaveData.LastUnlockedLevel >= level) return;

            _currentSaveData.LastUnlockedLevel = level;
        }

        public void SaveLastPassedLevel(int level)
        {
            if (level < 1)
            {
                Log.Gameplay.Error("Level must be positive to save.");
                return;
            }

            _currentSaveData.LastPassedLevel = level;
        }

        public void SetMusicVolume(float volume)
        {
            _currentSaveData.Settings.MusicVolume = volume;
        }

        public void SetSfxVolume(float volume)
        {
            _currentSaveData.Settings.SfxVolume = volume;
        }

        public void SetSystemCursorState(bool enabled)
        {
            _currentSaveData.Settings.IsSystemCursor = enabled;
        }

        public void SetFullscreenState(bool enabled)
        {
            _currentSaveData.Settings.IsFullScreen = enabled;
        }

        public void SetGameMode(GameMode gameMode)
        {
            if (_currentSaveData.ProfileData.GameMode != gameMode)
            {
                _currentSaveData.ProfileData.GameMode = gameMode;
            }
        }
        
        public void SetTutorialActive(bool isActive)
        {
            _currentSaveData.ProfileData.TutorialActive = isActive;
        }

        public void SaveLocalization(string language)
        {
            _currentSaveData.Settings.Language = language;
        }

        public void SaveLevel(int levelNumber, int starsCount)
        {
            if (levelNumber < 1)
            {
                Log.Gameplay.Error("Level must be positive to save.");
                return;
            }

            var savedLevel = GetDictionaryValue(_currentSaveData.Levels, levelNumber);

            SaveDictionaryValue(
                dictionary: _currentSaveData.Levels,
                key: levelNumber,
                value: new LevelData
                {
                    BestGameMode = _currentSaveData.ProfileData.GameMode,
                    StarsCount = savedLevel != null 
                        ? Math.Max(savedLevel.StarsCount, starsCount) 
                        : starsCount
                },
                updateAction: (existing, incoming) =>
                {
                    existing.StarsCount = incoming.StarsCount;

                    Log.Gameplay.Info($"Updated level {levelNumber} with stars {existing.StarsCount}.");
                },
                addAction: () =>
                {
                    Log.Gameplay.Info($"Saved level {levelNumber} with {starsCount} stars.");
                }
            );
        }

        public LevelData GetLevel(int levelNumber)
        {
            return GetDictionaryValue(_currentSaveData.Levels, levelNumber);
        }
        public void SaveAchievement(AchievementSaveData achievement)
        {
            if (string.IsNullOrEmpty(achievement.AchievementName))
            {
                Log.Gameplay.Error("Achievement name cannot be empty.");
                return;
            }
        
            SaveDictionaryValue(
                dictionary: _currentSaveData.Achievements,
                key: achievement.AchievementName,
                value: achievement,
                updateAction: (existing, incoming) =>
                {
                    existing.AchievementStatus = incoming.AchievementStatus;
            
                    // Log.Gameplay.Info($"Updated achievement {achievement.AchievementName} with status {achievement.AchievementStatus}.");
                },
                addAction: () =>
                {
                    // Log.Gameplay.Info($"Saved achievement {achievement.AchievementName} with status {achievement.AchievementStatus}.");
                }
            );
        }
        public bool TryGetAchievement(string achievementName, out AchievementSaveData achievement)
        {
            return _currentSaveData.Achievements.TryGetValue(achievementName, out achievement);
        }

        public void SaveComics(string comicsName)
        {
            if (string.IsNullOrEmpty(comicsName))
            {
                Log.Gameplay.Error("Comics name cannot be empty.");
                return;
            }

            if (_currentSaveData.PassedComics.Contains(comicsName))
            {
                return;
            }

            _currentSaveData.PassedComics.Add(comicsName);
            Log.Gameplay.Info($"Added passed comics: {comicsName}");
        }
        
        public void DeleteComics(string comicsName)
        {
            if (string.IsNullOrEmpty(comicsName))
            {
                Log.Gameplay.Error("Comics name cannot be empty.");
                return;
            }

            if (!_currentSaveData.PassedComics.Contains(comicsName))
            {
                return;
            }

            _currentSaveData.PassedComics.Remove(comicsName);
            Log.Gameplay.Info($"Remove passed comics: {comicsName}");
        }

        public bool IsComicsPassed(string comicsName)
        {
            if (string.IsNullOrEmpty(comicsName))
            {
                Log.Gameplay.Error("Comics name cannot be empty.");
                return false;
            }

            return _currentSaveData.PassedComics.Contains(comicsName);
        }

        public string GetLastPassedComics()
        {
            return _currentSaveData.PassedComics.Any() ? _currentSaveData.PassedComics.Last() : null;
        }

        public void SaveCollectionItem(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                Log.Gameplay.Error("Collection item name cannot be empty.");
                return;
            }

            if (_currentSaveData.CollectionItems.Contains(itemName))
            {
                return;
            }

            _currentSaveData.CollectionItems.Add(itemName);
            Log.Gameplay.Info($"Saved collection item: {itemName}");
        }

        public bool IsCollectionItemSaved(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                Log.Gameplay.Error("Collection item name cannot be empty.");
                return false;
            }

            // return false; who committed this?!
            return _currentSaveData.CollectionItems.Contains(itemName);
        }

        private void SaveDictionaryValue<TKey, TValue>(
            Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value,
            Action<TValue, TValue> updateAction = null,
            Action addAction = null)
        {
            if (dictionary.ContainsKey(key))
            {
                if (updateAction != null)
                {
                    updateAction(dictionary[key], value);
                }
                else
                {
                    dictionary[key] = value;
                }
            }
            else
            {
                dictionary.Add(key, value);
                addAction?.Invoke();
            }
        }
        
        private TValue GetDictionaryValue<TKey, TValue>(
            Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue = default)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}