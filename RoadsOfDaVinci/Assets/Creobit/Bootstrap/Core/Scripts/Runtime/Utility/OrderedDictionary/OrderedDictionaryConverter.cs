using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Utility.OrderedDictionary
{
    /// <summary>
    /// A custom JSON converter for serializing and deserializing an <see cref="OrderedDictionary{TKey, TValue}"/>.
    /// This converter ensures that the order of elements in the dictionary is preserved during serialization
    /// and deserialization by converting the dictionary into a list of key-value pairs.
    /// 
    /// During serialization, the dictionary is transformed into a list of key-value pairs, which is then serialized.
    /// During deserialization, the JSON is read as a list of key-value pairs, and the dictionary is reconstructed
    /// while maintaining the original order of elements.
    /// </summary>
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