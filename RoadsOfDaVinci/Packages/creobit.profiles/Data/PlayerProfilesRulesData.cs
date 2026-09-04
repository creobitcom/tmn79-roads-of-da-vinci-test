using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data
{
    [System.Serializable]
    public class PlayerProfilesRulesData
    {
        [field: Range(1, 11)]
        [field: SerializeField]
        public int MaxNameLength { get; private set; }
        [field: SerializeField] public PlayerProfileData DefaultProfile { get; private set; }
    }
}