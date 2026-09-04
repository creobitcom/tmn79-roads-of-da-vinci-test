namespace Creobit.EditionsUpgrade
{
    [System.Serializable]
    public class Tester
    {
        public string FullName { get; private set; }

        public string DeviceID { get; private set; }

        public Tester(string fullName, string deviceID)
        {
            FullName = fullName;
            DeviceID = deviceID;
        }
    }
}
