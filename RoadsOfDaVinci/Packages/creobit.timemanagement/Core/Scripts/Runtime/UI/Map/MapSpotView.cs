using System;
using TMPro;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI.Map
{
    public class MapSpotView : MonoBehaviour, IReactToClick
    {
        [SerializeField] private GameObject _tape;
        [SerializeField] private SpriteRenderer[] _stars; 

        public Transform rect;
        public int levelNum;
        public SpriteRenderer image;

        public event Action<MapSpotView> OnReact;

        public bool Interactable { get; private set; } = true;

        private bool _isStarsSorted;

        private void Awake()
        {
            var text = GetComponentInChildren<TMP_Text>();
            if (text)
            {
                text.text = levelNum.ToString();
            }

            EnsureStarsSorted();
        }

        public void OnClick()
        {
            if (!Interactable) return;

            OnReact?.Invoke(this);
        }

        public void SetInteractable(bool value)
        {
            Interactable = value;
        }

        public void SetStars(int valueStarsCount)
        {
            if (_tape != null)
            {
                _tape.SetActive(valueStarsCount > 0);
            }

            EnsureStarsSorted();

            if (_stars == null) return;

            for (int i = 0; i < _stars.Length; i++)
            {
                if (_stars[i] != null)
                {
                    _stars[i].gameObject.SetActive(i < valueStarsCount);
                }
            }
        }

        private void EnsureStarsSorted()
        {
            if (_isStarsSorted) return;
            _isStarsSorted = true;

            if (_stars != null && _stars.Length > 1)
            {
                Array.Sort(_stars, (a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    return a.transform.localPosition.x.CompareTo(b.transform.localPosition.x);
                });
            }
        }
    }
}