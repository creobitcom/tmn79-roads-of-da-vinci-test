using System;
using UltEvents;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController
{
    public class PathNode : MonoBehaviour
    {
        [SerializeField] public UltEvent onBlocked;
        [SerializeField] public UltEvent onAwake;
        [SerializeField] public UltEvent onRestart;

        public void OnAwake()
        {
            onRestart?.Invoke();
            onAwake?.Invoke();
        }
    }
}