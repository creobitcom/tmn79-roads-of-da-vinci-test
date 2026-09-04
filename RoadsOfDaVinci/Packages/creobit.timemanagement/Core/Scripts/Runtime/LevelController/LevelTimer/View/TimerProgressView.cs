using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.View
{
    public class TimerProgressView : MonoBehaviour, IProgress<float>
    {
        private enum LostStarBehaviour
        {
            Hide,
            ReplaceSprite
        }

        [SerializeField] 
        private Image _progressImage;

        [SerializeField]
        private ushort _warningThreshold;

        [SerializeField]
        private UltEvent _starWarning;

        [SerializeField]
        private UltEvent _starDissapeared;

        [Header("Star Visuals")]
        [SerializeField]
        [Tooltip("Moves every star across the timer without changing its time position.")]
        private float _starCrossAxisOffset;

        [SerializeField]
        [Tooltip("Additional cross-axis offsets for individual stars, in Level Stars Data order.")]
        private float[] _starCrossAxisOffsets = new float[3];

        [SerializeField]
        [Min(0.01f)]
        private float _starScale = 1f;

        [SerializeField]
        private LostStarBehaviour _lostStarBehaviour;

        [SerializeField]
        private Sprite _lostStarSprite;

        [Header("Easy Mode")]
        [SerializeField]
        [Tooltip("Optional. When set, the timer stays visible in easy mode with this fill image instead of the default one, without stars, instead of being hidden.")]
        private Image _easyModeProgressImage;

        private uint _maxLevelTime;

        private LevelStarData _levelStarData;

        private Dictionary<Image, ushort> _stars = new Dictionary<Image, ushort>();

        private Dictionary<Image, Vector3> _starBaseScales = new Dictionary<Image, Vector3>();

        private HashSet<Image> _lostStars = new HashSet<Image>();

        private bool _starAffected;

        private bool _easyMode;

        public Image CurrentAffectedStar { get; private set; }

        public bool HasEasyModeVisuals => _easyModeProgressImage != null;

        private Image ActiveProgressImage => _easyMode ? _easyModeProgressImage : _progressImage;

        public void SetEasyMode(bool isEasyMode)
        {
            _easyMode = isEasyMode && HasEasyModeVisuals;

            if (!HasEasyModeVisuals)
            {
                return;
            }

            _easyModeProgressImage.gameObject.SetActive(_easyMode);
            _progressImage.gameObject.SetActive(!_easyMode);
        }

        public void SetData(LevelTimerData levelTimerData)
        {
            _maxLevelTime = (uint)(levelTimerData.GameplayIntervalGeneralParameters.DurationMilliseconds * 0.001f);

            if (_easyMode)
            {
                return;
            }

            SpawnStars(levelTimerData.LevelStarsData, levelTimerData.Orientation).Forget();
        }

        private async UniTask SpawnStars(LevelStarData[] levelStarData, Orientation orientation)
        {
            var timerTransform = _progressImage.transform;

            var currentLength = orientation == Orientation.Horizontal ?
                _progressImage.rectTransform.rect.width :
                _progressImage.rectTransform.rect.height;

            var currentVector = orientation == Orientation.Horizontal ? Vector3.right : Vector3.up;

            var crossAxisVector = orientation == Orientation.Horizontal ? Vector3.up : Vector3.right;

            for (var starIndex = 0; starIndex < levelStarData.Length; starIndex++)
            {
                await UniTask.Yield();

                var starData = levelStarData[starIndex];
                
                var star = Instantiate(starData.StarPrefab, timerTransform);
                var starTime = (ushort) (_maxLevelTime - starData.TimeLeftMoreThan);

                _stars.Add(star, starTime);

                //Find the length on the timer corresponding to one second
                var ratio = _maxLevelTime / currentLength;
                //Find the star's position along the timer
                var currentPoint = starTime / ratio;
                //Adjust the position so it matches the timer's center pivot
                currentPoint = currentPoint - currentLength / 2;

                var individualCrossAxisOffset = _starCrossAxisOffsets != null &&
                                                starIndex < _starCrossAxisOffsets.Length
                    ? _starCrossAxisOffsets[starIndex]
                    : 0f;

                star.transform.localPosition = currentVector * currentPoint +
                                               crossAxisVector * (_starCrossAxisOffset + individualCrossAxisOffset);

                // New serialized fields can be zero in prefabs created before this option existed.
                // Treat that value as the legacy scale so old games keep their current visuals.
                var starScale = _starScale <= 0f ? 1f : _starScale;
                _starBaseScales.Add(star, star.transform.localScale);
                star.rectTransform.sizeDelta *= starScale;
            }
        }

        public void ResetTimer()
        {
            ResetStars();
            _starAffected = false;
            CurrentAffectedStar = null;
            Report(_maxLevelTime);
        }

        private void ResetStars()
        {
            foreach(var starData in _stars)
            {
                Destroy(starData.Key.gameObject);
            }
            _stars.Clear();
            _starBaseScales.Clear();
            _lostStars.Clear();
        }

        public void Report(float value)
        {
            UpdateProgressBar(value);
            CheckStarsStatus(value);
        }

        private void UpdateProgressBar(float value)
        {
            ActiveProgressImage.fillAmount = value / _maxLevelTime;
        }

        private void CheckStarsStatus(float value)
        {
            foreach (var starData in _stars)
            {
                int warningThreshold = starData.Value + _warningThreshold;
                int disappearThreshold = starData.Value;
                
                // If the star is already lost, skip checking it. A lost star can remain active
                // when it is displayed with a replacement sprite.
                if (_lostStars.Contains(starData.Key) || !starData.Key.gameObject.activeSelf)
                    continue;

                // Check for warning state
                if (value < warningThreshold && value > disappearThreshold)
                {
                    if (!_starAffected)
                    {
                        TriggerStarWarning(starData.Key);
                        break;
                    }
                }
                // Check for disappeared state
                else if (value < disappearThreshold)
                {
                    TriggerStarDisappear(starData.Key);
                    break;
                }
            }
        }

        private void TriggerStarWarning(Image star)
        {
            CurrentAffectedStar = star;
            _starAffected = true;
            _starWarning?.Invoke();
        }

        private void TriggerStarDisappear(Image star)
        {
            _lostStars.Add(star);

            if (_lostStarBehaviour == LostStarBehaviour.ReplaceSprite && _lostStarSprite != null)
            {
                var animator = star.GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = false;

                if (_starBaseScales.TryGetValue(star, out var baseScale))
                    star.transform.localScale = baseScale;

                star.sprite = _lostStarSprite;
            }
            else
            {
                star.gameObject.SetActive(false);
            }

            CurrentAffectedStar = star;
            _starAffected = false;
            _starDissapeared?.Invoke();
        }

        public void SpawnEffectOnStar(GameObject effect)
        {
            Vector3 position = Camera.main.ScreenToWorldPoint(CurrentAffectedStar.transform.position);
            position.z = 60;
            Instantiate(effect, position, Quaternion.identity);
        }

        public void SetAnimatorTriggerOnStar(string trigger)
        {
            CurrentAffectedStar.GetComponent<Animator>().SetTrigger(trigger);
        }

    }
}
