using System;
using Creobit.Loading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Creobit.UI
{
    public class PanelData : MonoBehaviour, ILoadUnit, IDisposable
    {
        public PanelAnimationData[] PanelAnimations;

        [Header("Safe area padding")] 
        public bool Right;
        public bool Left;

        public virtual UniTask Load() => UniTask.CompletedTask;

        public virtual void Dispose() { }
    }
}