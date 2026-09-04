using System.Collections.Generic;
using _8floor.TimeManagement.Artifacts.Runtime.Data;
using Creobit.Loading;

namespace _8floor.TimeManagement.Artifacts.Runtime.Service
{
    public interface IArtifactsService : ILoadUnit
    {
        public IReadOnlyList<ArtifactDataSO> Artifacts { get; }

        /// <summary>
        /// Initializes and populates the list of available artifacts based on references from BootstrapPrefabReferences 
        /// and the current player profile settings. Artifacts marked as CollectorOnly are only included during 
        /// compilation with the COLLECTOR symbol defined.
        /// </summary>
        public IReadOnlyList<ArtifactDataSO> AvailableArtifacts { get; }
        
        /// <summary>
        /// Checks whether a specific artifact part has been received.
        /// </summary>
        /// <param name="partName">The name of the artifact part to check.</param>
        /// <returns>True if the part is received; otherwise, false.</returns>
        public bool PartIsReceived(string partName);

        /// <summary>
        /// Determines whether the specified artifact is available in the current build.
        /// If the artifact is marked as CollectorOnly, returns true only if the COLLECTOR symbol is defined; otherwise false.
        /// </summary>
        /// <param name="artifact">Artifact data to check.</param>
        /// <returns>True if the artifact is available; otherwise false.</returns>
        public bool ArtifactIsAvailable(ArtifactDataSO artifact);

        /// <summary>
        /// Checks whether all parts of ALL (not only available) artifacts have been collected by the current player profile.
        /// </summary>
        /// <returns>True if ALL (not only available) artifact parts are collected; otherwise, false.</returns>
        public bool AllArtifactsIsCollected();
    }
}
