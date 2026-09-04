using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.Reload;
using Creobit.Loading;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Bubbles
{
    public interface IBubbleController : ILoadUnit, IReloadable, IDisposable
    {
        void ShowSequence(BubbleSequenceSO sequence, Transform anchor, Action onComplete = null);
        void Show(BubbleData bubbleData, Transform anchor);
        void Hide(BubbleData bubbleData);
        void HideCurrent();
    }
}