using System;
using Creobit.Loading;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayInputSystem
{
    public interface IMetaInputSystem : ILoadUnit<Camera>, IDisposable
    {
        event Action OnMainButtonPressed;
        event Action OnBackButtonPressed;
        event Action OnGameDeviceChanged;
        event Action<Vector2> OnSecondaryActionHold;
        event Action<float> OnSwipeVertical;
        
        void Enable();
        void Disable();
        GameDevice GetActiveGameDevice();
    }
}