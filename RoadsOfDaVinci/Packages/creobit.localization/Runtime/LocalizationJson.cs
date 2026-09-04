using System.Collections.Generic;
using Newtonsoft.Json;

namespace Creobit.Localization
{
    /// <summary>
    /// Represents a localization data structure where key-value pairs of string data are stored.
    /// This class is used to hold localization text for different identifiers, typically loaded
    /// and constructed from external sources (e.g., JSON files).
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class LocalizationJson
    {
        /// <summary>
        /// A dictionary that stores localization data as key-value pairs.
        /// The keys are typically unique identifiers for text, while the values
        /// represent the localized strings associated with those identifiers.
        /// </summary>
        public Dictionary<string, string> Data;
    }
}