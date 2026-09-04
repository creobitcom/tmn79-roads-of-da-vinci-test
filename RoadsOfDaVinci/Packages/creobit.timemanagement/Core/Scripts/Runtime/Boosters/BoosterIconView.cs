using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Boosters
{
    [RequireComponent(typeof(Image))]
    public class BoosterIconView : MonoBehaviour
    {
        [Serializable]
        private struct BoosterImageStruct
        {
            public string Id;
            public Sprite Sprite;
        }

        [SerializeField] private List<BoosterImageStruct> _boosterImages = new List<BoosterImageStruct>();
        [SerializeField] private BoosterView _boosterView;
        [SerializeField] private Image _image;
        
        private void Awake()
        {
            _image ??= GetComponent<Image>();
        }

        private void OnEnable()
        {
            // UpdateImage();
        }

        public void UpdateImage()
        {
            _image ??= GetComponent<Image>();
            // if (_image == null || _boosterView == null || _boosterView.BoosterData == null || _boosterView.BoosterData.Id == null) return;
            var sprite = _boosterImages.First(x => x.Id == _boosterView.BoosterData.Id).Sprite;
            if (sprite != null)
            {
                _image.sprite = sprite;
            }
        }
    }
}