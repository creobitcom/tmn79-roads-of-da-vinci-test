using System;
using _8floor.TimeManagement.Core.Scripts.Runtime.Loader;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayUtils
{
    [Serializable]
    [HideLabel]
    [InlineProperty]
    public class LoaderSceneReferences
    {
        [field: SerializeField]
        public Transform LoadingScreenTransform { get; private set; }

        [field: SerializeField]
        public LoadingView LoadingView { get; private set; }
    }
}