using System;
using DG.Tweening;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public class Rotatable : MonoBehaviour
    {
        private float _rotation;

        private void Awake()
        {
            _rotation = transform.rotation.eulerAngles.z;
        }

        public void Rotate(float angle, float duration = 1)
        {
            _rotation += angle;
            
            transform.DOKill();
            transform.DORotate(new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 
                _rotation), duration);
        }
    }
}