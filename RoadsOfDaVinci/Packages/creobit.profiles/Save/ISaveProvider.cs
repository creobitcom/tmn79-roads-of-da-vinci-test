using Creobit.Loading;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    public interface ISaveProvider : ILoadUnit
    {
        public void Save(string key, string value);
        public string TryGetValue(string key, string defaultValue = "");
        public void DeleteAll();
    }
}