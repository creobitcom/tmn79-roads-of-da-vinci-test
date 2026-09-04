using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils.DoTweenAnimationExtensions
{
    public class DoTweenJumpAnimation : MonoBehaviour
    {
        [SerializeField]
        private float time;

        [SerializeField]
        private float power;
        
        [SerializeField]
        private Transform targetToMove;
        
        [SerializeField]
        private Transform targetFrom;

        [Button]
        public void Play()
        {
            targetToMove.position = targetFrom.position;
            
            targetToMove
                .DOLocalJump(Vector3.zero, power, 1, time)
                .SetUpdate(true);
        }
    }
}