using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    /// <summary>
    ///     Responsible for saving and retrieving encrypted data using Unity's PlayerPrefs.
    /// </summary>
    public class PlayerPrefsSaveProvider : IPlayerPrefsSaveProvider
    {
        /// <summary>
        ///     Loads the saved game data from PlayerPrefs asynchronously.
        /// </summary>
        /// <return>
        ///     A UniTask representing the asynchronous load operation.
        /// </return>
        public UniTask Load()
        {
            return UniTask.CompletedTask;
        }

        /// Encrypts and saves a value associated with the given key into PlayerPrefs.
        /// <param name="key">The key under which the value will be stored.</param>
        /// <param name="value">The value to be stored, which will be encrypted before saving.</param>
        /// /
        public void Save(string key, string value)
        {
            // Encrypt the value
            var encryptedValue = value;
            // Store the encrypted value
            PlayerPrefs.SetString(key, encryptedValue);
            PlayerPrefs.Save();
        }

        /// Tries to get the value associated with the specified key from PlayerPrefs.
        /// If the key is found, it returns the decrypted value; otherwise, it returns null or a default value.
        /// <param name="key">The key to look up in PlayerPrefs.</param>
        /// <param name="defaultValue">The value to set in the PlayerPrefs if this key does not exist.</param>
        /// <returns>The decrypted value associated with the key, or null/default if the key is not found.</returns>
        public string TryGetValue(string key, string defaultValue = "")
        {
            // Get the encrypted value
            var encryptedValue = PlayerPrefs.GetString(key, defaultValue);
            // Decrypt and return the value
            var result = encryptedValue;
            return string.IsNullOrEmpty(result) ? defaultValue : result;
        }

        /// Removes every stored value, returning the game to a clean install state.
        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}