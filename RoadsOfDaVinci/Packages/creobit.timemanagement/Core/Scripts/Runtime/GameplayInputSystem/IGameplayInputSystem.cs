using System;
using Creobit.Loading;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem
{
    public interface IGameplayInputSystem : ILoadUnit, IDisposable
    {
        public void Enable();
        public void Disable();
        public GameDevice GetActiveGameDevice();
        public event Action OnMainButtonPressed;

        /// <summary>
        /// Игрок успешно нажал на объект уровня. Нужно туториалу, чтобы листать стадии
        /// по действию игрока и не требовать проводки событий на каждом объекте.
        /// </summary>
        public event Action<ObjectView.MovableObjects.TaskManager.IReactToActions> PrimaryActionPerformed;

        /// <summary>
        /// Объект, чей OnPrimaryAction выполняется прямо сейчас. Нужен вызываемым из UltEvent методам,
        /// чтобы отличать повторный клик по одному объекту от клика по другому.
        /// </summary>
        public ObjectView.MovableObjects.TaskManager.IReactToActions CurrentPrimaryActionTarget { get; }
        public event Action OnGameDeviceChanged;
        public event Action OnPauseButtonPressed;
        public event Action<float> OnZoom;
        public bool IsActionAvailable { get; set; }
    }
}
