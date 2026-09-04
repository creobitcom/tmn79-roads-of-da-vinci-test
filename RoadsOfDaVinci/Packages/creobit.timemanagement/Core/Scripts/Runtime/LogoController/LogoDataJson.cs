using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LogoController
{
    [Preserve, Serializable]
    public class LogoDataJson
    {
        [JsonProperty("currentEdition")]
        public string CurrentEdition { get; set; }

        [Preserve]
        public LogoDataJson(string currentEdition)
        {
            CurrentEdition = currentEdition;
        }
    }
}