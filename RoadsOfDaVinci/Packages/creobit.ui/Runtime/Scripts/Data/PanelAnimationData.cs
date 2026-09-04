using System;
using Creobit.UI.Utility;
using DG.Tweening;
using UnityEngine;

namespace Creobit.UI
{
    [Serializable]
    public struct PanelAnimationData
    {
        [field: SerializeField]
        public PanelState PanelState { get; private set; }

        [field: SerializeField]
        public DOTweenAnimation AnimationPrefab { get; private set; }

        [field: SerializeField]
        public bool IsSequence { get; private set; }
    }
}