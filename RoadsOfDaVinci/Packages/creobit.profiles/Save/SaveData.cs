using System;
using System.Collections.Generic;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Localization;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    [Serializable]
    public class SaveData
    {
        public string ProfileName;
        public int LastUnlockedLevel;
        public int LastPassedLevel;
        public readonly GameSettings Settings;
        public readonly PlayerProfileData ProfileData;
        public readonly Dictionary<string, AchievementSaveData> Achievements;
        public readonly Dictionary<int, LevelData> Levels;        
        public readonly List<string> PassedComics;
        public readonly HashSet<string> CollectionItems;

        public SaveData(
            GameSettings settings,
            int lastUnlockedLevel,
            int lastPassedLevel,
            PlayerProfileData profileData,
            Dictionary<string, AchievementSaveData> achievements,
            Dictionary<int, LevelData> levels,
            List<string> passedComics,
            HashSet<string> collectionItems)
        {
            Settings = settings;
            LastUnlockedLevel = lastUnlockedLevel;
            LastPassedLevel = lastPassedLevel;

            ProfileData = profileData;
            ProfileName = profileData.Name;
            Achievements = achievements;
            Levels = levels;
            PassedComics = passedComics;
            CollectionItems = collectionItems;
        }

        public static SaveData Create(PlayerProfileData profileData)
        {
            var language = LocalizationService.Instance.CurrentLanguage;
            if (string.IsNullOrEmpty(language))
                language = "en-US";

            return new SaveData(
                settings:             new(0.5f, 0.5f, true, false, language),
                lastUnlockedLevel:    1,
                lastPassedLevel:      0,
                profileData:          profileData,
                achievements:         new(),
                levels:               new(),
                passedComics:         new(),
                collectionItems:      new()
            );
        }

        public SaveData Clone()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SaveData>(json);
        }
    }

    [Serializable]
    public class LevelData
    {
        public GameMode BestGameMode;
        public int StarsCount;
    }
    
    public class GameSettings
    {
        [field: SerializeField]
        public float MusicVolume { get; set; }

        [field: SerializeField]
        public float SfxVolume { get; set; }

        [field: SerializeField]
        public bool IsFullScreen { get; set; }

        [field: SerializeField]
        public bool IsSystemCursor { get; set; }
        
        [field: SerializeField]
        public string Language { get; set; }
        
        public GameSettings(float musicVolume,float sfxVolume,bool isFullScreen,bool isSystemCursor,string language)
        {
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            IsFullScreen = isFullScreen;
            IsSystemCursor = isSystemCursor;
            Language = language;
        }
    }
    
    [Serializable]
    public class AchievementSaveData
    {
        public string AchievementName;
        public string AchievementStatus;

        public AchievementSaveData(string achievementName, string achievementStatus)
        {
            AchievementName = achievementName;
            AchievementStatus = achievementStatus;
        }
    }
}