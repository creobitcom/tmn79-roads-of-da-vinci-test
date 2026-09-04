using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Creobit.Bootstrap.Core.Scripts.Runtime.EventsInterceptors;
using Creobit.Bootstrap.Core.Scripts.Runtime.Operation;
using Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Data;
using Creobit.Bootstrap.Core.Scripts.Runtime.Save;
using Creobit.Loading;
using Creobit.Localization;
using Creobit.Logger;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Profiles.Controller
{
    public class PlayerProfilesService : ILoadUnit<PlayerProfilesRulesData>, IDisposable
    {
        public class RuntimeConstants
        {
            public static class PlayerProfilesPrefs
            {
                public const string Profiles = "PlayerProfiles";
                public const string FirstProfile = "FirstProfile";
                public const string FirstDefaultProfileName = "NewUser";
            }

            public static class PlayerProfilesRules
            {
                public const int MaxProfilesAmount = 5;
                public const string NamePattern = @"^[\p{L}0-9]+$";
            }

            public static class SaveItems
            {
                public const string CurrentLevel = "CurrentLevel";
            }
        }
        
        private PlayerProfilesData _profiles;

        private IPlayerPrefsSaveProvider _playerPrefsProvider;
        private ISaveController _saveController;
        private PlayerProfilesRulesData _rules;
        private ApplicationEventsInterceptor _applicationEventsInterceptor;

        /// <summary>
        /// Checks whether there are any player profiles available in the system.
        /// Returns <c>true</c> if the collection of player profiles contains one or more profiles, 
        /// and <c>false</c> if the collection is empty.
        /// </summary>
        public bool ProfilesIsExists => _profiles.Profiles.Count > 0;

        public bool IsMaxProfilesReached => _profiles.Profiles.Count >= RuntimeConstants.PlayerProfilesRules.MaxProfilesAmount;

        public bool IsProfilesCountLow => _profiles.Profiles.Count <= 1;

        public string SelectedProfileName => _profiles.SelectedProfileName;

        public IEnumerable<PlayerProfileData> Profiles => _profiles.Profiles.Values;

        public PlayerProfileData CurrentProfile
        {
            get
            {
                if(_profiles.Profiles.ContainsKey(_profiles.SelectedProfileName))
                {
                    return _profiles.Profiles[_profiles.SelectedProfileName];
                }
                else
                {
                    return new PlayerProfileData();
                }
            }
        }

        public event Action<PlayerProfileData> AddedProfile;
        public event Action<PlayerProfileData> SelectedProfile;
        public event Action<PlayerProfileData> RemovedProfile;
#if ALL_IN_ONE
        private GameSwitcher _gameSwitcher;
#endif     
        public string GameName { get; private set; }
        [Inject]
        private void Construct(
            IPlayerPrefsSaveProvider playerPrefsProvider,
            ISaveController saveController,
#if ALL_IN_ONE
            GameSwitcher gameSwitcher,
#endif     
            ApplicationEventsInterceptor applicationEventsInterceptor)            
        {
            _playerPrefsProvider = playerPrefsProvider;
            _saveController = saveController;
#if ALL_IN_ONE
            _gameSwitcher = gameSwitcher;
#endif     
            _applicationEventsInterceptor = applicationEventsInterceptor;
        }

        public UniTask Load(PlayerProfilesRulesData rules)
        {
            _rules = rules;
            _applicationEventsInterceptor.Quitting += OnQuitting;

            _applicationEventsInterceptor.PauseStateChanged += OnPauseStateChanged;
            _applicationEventsInterceptor.FocusStateChanged += OnFocusStateChanged;

            string profilesJson = _playerPrefsProvider.TryGetValue(RuntimeConstants.PlayerProfilesPrefs.Profiles);   
#if ALL_IN_ONE
            GameName = _gameSwitcher.CurrentGame.Value.GameName;
#endif     
            
            // If no profiles exist, initialize a new empty profile collection and save it.
            if (string.IsNullOrEmpty(profilesJson))
            {
                Log.Bootstrap.Warning($"{nameof(PlayerProfilesData)} not found.Creating a new one. If this is not first time launching consider it as an error");
                
                _profiles = new PlayerProfilesData("NewUser",new OrderedDictionary<string, PlayerProfileData>())
                {
                    Profiles = new OrderedDictionary<string, PlayerProfileData>()
                };

                ResetProfiles();
                AddProfile(RuntimeConstants.PlayerProfilesPrefs.FirstDefaultProfileName);
                _playerPrefsProvider.Save(RuntimeConstants.PlayerProfilesPrefs.FirstProfile, "TRUE");
                profilesJson = _playerPrefsProvider.TryGetValue(RuntimeConstants.PlayerProfilesPrefs.Profiles);
            }

            try
            {
                Log.Bootstrap.Info($"Deserializing {nameof(PlayerProfilesData)}...");
                _profiles = JsonConvert.DeserializeObject<PlayerProfilesData>(profilesJson);                
            }
            catch (Exception ex)
            {
                ResetProfiles();
                AddProfile(RuntimeConstants.PlayerProfilesPrefs.FirstDefaultProfileName);
                Log.Bootstrap.Error($"An error occurred while deserializing {nameof(PlayerProfilesData)}: {ex}");
            }           

            _saveController.LoadData();

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _applicationEventsInterceptor.Quitting -= OnQuitting;

            _applicationEventsInterceptor.PauseStateChanged -= OnPauseStateChanged;
            _applicationEventsInterceptor.FocusStateChanged -= OnFocusStateChanged;
        }

        /// <summary>
        /// Adds a new player profile with the specified name.
        /// Ensures that:
        /// - The profile name is unique (does not already exist in the collection).
        /// - The maximum number of allowed profiles has not been reached.
        /// - The profile name is not null, empty, or exceeds the maximum allowed length.
        /// - The profile name matches the required naming pattern (e.g., no special characters).
        /// If any of these conditions are not met, the method exits without adding the profile.
        /// </summary>
        public OperationResult AddProfile(string name)
        {
            name = name.Trim();
            if (IsMaxProfilesReached)
            {
                return new(false, "The maximum amount of profiles has been reached.");
            }

            if (_profiles.Profiles.ContainsKey(name))
            {
                return new(false, LocalizationService.Instance.GetText("profiles_dlg_exist"));
            }

            if (string.IsNullOrEmpty(name))
            {
                return new(false, LocalizationService.Instance.GetText("profiles_dlg_name_empty"));
            }

            if (name.Length > _rules.MaxNameLength)
            {
                return new(false, $"Name length must not exceed {_rules.MaxNameLength} characters.");
            }

            if (!Regex.IsMatch(name, RuntimeConstants.PlayerProfilesRules.NamePattern))
            {
                return new(false, LocalizationService.Instance.GetText("profiles_dlg_special_char_error"));
            }

            if (string.IsNullOrEmpty(SelectedProfileName) == false)
            {
                _saveController.Save();   
            }            

            PlayerProfileData profile = _rules.DefaultProfile.Clone(name);

            _profiles.Profiles.Add(name, profile);

            AddedProfile?.Invoke(profile);

            SelectProfileWithoutValidation(profile);

            SaveProfiles();                        

            _saveController.Service?.CreateProfileSave(profile);            

            _saveController.LoadData();

            return new(true, string.Empty);
        }

        /// <summary>
        /// Selects a player profile by name.
        /// Validates that the profile exists before selecting it.
        /// </summary>
        public void SelectProfile(string name)
        {
            if (!_profiles.Profiles.ContainsKey(name))
            {
                return;
            }

            SelectProfileWithoutValidation(_profiles.Profiles[name]);            
        }

        /// <summary>
        /// Removes the currently selected player profile.
        /// Ensures that at least one profile remains after removal.
        /// </summary>
        public void RemoveSelectedProfile()
        {
            // Ensure the current profile exists and that there are at least two profiles so that at least one profile remains when deleting
            if (!_profiles.Profiles.ContainsKey(_profiles.SelectedProfileName)
                || IsProfilesCountLow)
            {
                return;
            }

            PlayerProfileData profile = _profiles.Profiles[_profiles.SelectedProfileName];

            _profiles.Profiles.Remove(_profiles.SelectedProfileName);

            RemovedProfile?.Invoke(profile);

            string lastProfileName = _profiles.Profiles.GetKeyByIndex(_profiles.Profiles.Count - 1);

            PlayerProfileData lastProfile = _profiles.Profiles[lastProfileName];

            if (lastProfile == null)
            {
                SaveProfiles();

                return;
            }

            SelectProfileWithoutValidation(lastProfile);

            SaveProfiles();
        }

        public void RemoveProfile(string name)
        {
            SelectProfile(name);
            RemoveSelectedProfile();
        }

        // Reset and save _profiles
        public void ResetProfiles()
        {
            _profiles = new(string.Empty, new OrderedDictionary<string, PlayerProfileData>());
            SaveProfiles();
        }

        // Allows to avoid unnecessary checks
        private void SelectProfileWithoutValidation(PlayerProfileData profile)
        {
            if (_profiles.Profiles.ContainsKey(_profiles.SelectedProfileName))
            {
                _saveController.Save();   
            }            

            _profiles.SelectedProfileName = profile.Name;

            _saveController.LoadData();

            SelectedProfile?.Invoke(profile);            
        }

        private void SaveProfiles()
        {
            _playerPrefsProvider.Save(RuntimeConstants.PlayerProfilesPrefs.Profiles, JsonConvert.SerializeObject(_profiles));
        }

        private void OnPauseStateChanged(bool pause)
        {
            if (!pause)
            {
                return;
            }

            SaveProfiles();
        }

        private void OnFocusStateChanged(bool focus)
        {
            if (!focus)
            {
                return;
            }

            SaveProfiles();
        }

        private void OnQuitting() => SaveProfiles();
    }
    
    public interface IReadOnlyOrderedDictionary<TKey, TValue>
    {
        public TValue this[TKey key] { get; }

        public int Count { get; }

        public bool ContainsKey(TKey key);

        public bool TryGetValue(TKey key, out TValue value);

        public void Remove(TKey key);

        public void Clear();

        public IEnumerable<TKey> Keys { get; }

        public IEnumerable<TValue> Values { get; }
    }

    public class OrderedDictionary<TKey, TValue> : IReadOnlyOrderedDictionary<TKey, TValue>,
        IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();

        private readonly List<TKey> _order = new();

        public void Add(TKey key, TValue value)
        {
            if (!_dictionary.TryAdd(key, value))
            {
                return;
            }

            _order.Add(key);
        }

        public void Prepend(TKey key, TValue value)
        {
            if (!_dictionary.TryAdd(key, value))
            {
                return;
            }

            _order.Insert(0, key);
        }

        public TValue this[TKey key] => _dictionary[key];

        public int Count => _dictionary.Count;

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            if (!ContainsKey(key))
            {
                return;
            }

            _dictionary.Remove(key);

            _order.Remove(key);
        }

        public void Clear()
        {
            _dictionary.Clear();

            _order.Clear();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public IEnumerable<TKey> Keys => _order;

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (var key in _order)
                {
                    yield return _dictionary[key];
                }
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var key in _order)
            {
                yield return new KeyValuePair<TKey, TValue>(key, _dictionary[key]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public TKey GetKeyByIndex(int index)
        {
            return _order[index];
        }
    }
    
    public class OrderedDictionaryConverter<TKey, TValue> : JsonConverter<OrderedDictionary<TKey, TValue>>
    {
        public override void WriteJson(JsonWriter writer, OrderedDictionary<TKey, TValue> value, JsonSerializer serializer)
        {
            var list = new List<KeyValuePair<TKey, TValue>>();

            foreach (var key in value.Keys)
            {
                list.Add(new KeyValuePair<TKey, TValue>(key, value[key]));
            }

            serializer.Serialize(writer, list);
        }

        public override OrderedDictionary<TKey, TValue> ReadJson(JsonReader reader, Type objectType,
            OrderedDictionary<TKey, TValue> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var list = serializer.Deserialize<List<KeyValuePair<TKey, TValue>>>(reader);

            var result = new OrderedDictionary<TKey, TValue>();

            if (list != null)
            {
                foreach (var pair in list)
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            return result;
        }
    }
}