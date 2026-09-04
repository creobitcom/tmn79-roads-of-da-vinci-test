using System;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Loader
{
    public class LoadingView : MonoBehaviour, IProgress<float>
    {
        [SerializeField] 
        private Image _progressImage;
        
        public void Report(float value)
        {
            _progressImage.fillAmount = value;
        }
    }
}