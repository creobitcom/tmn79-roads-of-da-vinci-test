using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data
{
    [Preserve]
    public class PlayerProfilesData
    {
        public string SelectedProfileName { get; set; }

        [JsonConverter(typeof(OrderedDictionaryConverter<string, PlayerProfileData>))]
        public OrderedDictionary<string, PlayerProfileData> Profiles { get; set; }
        [Preserve]
        public PlayerProfilesData(string selectedProfileName, OrderedDictionary<string, PlayerProfileData> profiles)
        {
            SelectedProfileName = selectedProfileName;
            Profiles = profiles;
        }
    }
}