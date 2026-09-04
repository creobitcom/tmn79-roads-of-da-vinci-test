using System;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Settings
{
    [System.Serializable]
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

        public bool IsInitialized()
        {
            return MusicVolume == default
                && SfxVolume == default
                && IsFullScreen == default
                && IsSystemCursor == default;
        }

        public GameSettings(float musicVolume,float sfxVolume,bool isFullScreen,bool isSystemCurso,string language)
        {
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            IsFullScreen = isFullScreen;
            IsSystemCursor = isSystemCurso;
            Language = language;
        }
    }
}