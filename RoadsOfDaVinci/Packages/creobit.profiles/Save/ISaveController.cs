using System;
using Creobit.Loading;

namespace Creobit.Bootstrap.Core.Scripts.Runtime.Save
{
    /// <summary>
    /// Interface for managing save operations in the application.
    /// </summary>
    public interface ISaveController : ILoadUnit, IDisposable
    {
        /// <summary>
        /// Gets the current save data instance.
        /// </summary>
        /// <value>
        /// The <see cref="SaveData"/> object representing the current state of the saved data.
        /// </value>
        SaveData CurrentSaveData { get; }

        /// <summary>
        /// Service that implements the logic for saving data.
        /// </summary>
        SaveService Service { get; }

        /// <summary>
        /// Saves all current game data.
        /// </summary>
        void Save();

        /// <summary>
        /// Reset all current game data.
        /// </summary>
        void Reset();

        /// <summary>
        /// Loads all saved game data
        /// </summary>
        void LoadData();
    }
}