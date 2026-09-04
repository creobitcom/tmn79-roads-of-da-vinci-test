using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Serialization;
using Log = Creobit.Logger.Log;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data
{
    [System.Serializable, Preserve]
    public class PlayerProfileData
    {
        private GameMode _gameMode;

        [JsonProperty]
        public string Name { get; private set; } = "DefaultPlayer";
        [JsonProperty]
        public GameMode GameMode
        {
            get => _gameMode;
            set
            {
                if (value == _gameMode) return;
                _gameMode = value;
                Log.Bootstrap.Info($"{Name} has current game mode: {GameMode}");
            }
        }

        [JsonProperty] 
        public bool TutorialActive { get; set; } = true;

        public HashSet<string> ReceivedArtifactPartsNames { get; private set; } = new();

        public System.Collections.Generic.Dictionary<string, int> CollectiblesProgress { get; set; } = new();

        public PlayerProfileData Clone(string name)
        {
            var json = JsonConvert.SerializeObject(this);
            var result = JsonConvert.DeserializeObject<PlayerProfileData>(json);

            result.Name = name;
            if (result._gameMode!=GameMode.Medium)
            {
                result.GameMode = GameMode.Medium;
            }

            return result;
        }
    }
}