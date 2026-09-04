namespace _8floor.TimeManagement.Artifacts.Runtime.Providers
{
    public interface IArtifactProgressProvider
    {
        /// <summary>
        /// Checks whether a specific artifact part has been received.
        /// </summary>
        /// <param name="partName">The name of the artifact part to check.</param>
        /// <returns>True if the part is received; otherwise, false.</returns>
        public bool PartIsReceived(string partName);
    }
}
