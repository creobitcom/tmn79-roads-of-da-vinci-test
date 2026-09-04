using System;
using System.Collections.Generic;
using Creobit.Localization;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Cutscenes.UI
{
    /// <summary>
    /// Корень префаба комикс-сцены. Держит затемнение, список фото (ComicPhoto),
    /// кнопку закрытия с кольцом-прогрессом и параметры зума.
    /// Спавнится контроллером на время катсцены и уничтожается по завершении.
    /// </summary>
    public class ComicScene : MonoBehaviour
    {
        [Header("Background")]
        [SerializeField] private Image _darkBackground;

        [Header("Photos (порядок = порядок появления)")]
        [SerializeField] private List<ComicPhoto> _photos = new();

        [Header("Pop-up")]
        [Tooltip("Начальный масштаб кадра перед pop-up (1 = без увеличения).")]
        [SerializeField] private float _popStartScale = 0.6f;

        [Header("Close button")]
        [SerializeField] private Button _closeButton;
        [Tooltip("Кольцо прогресса закрытия (Image type = Filled). Заполняется за суммарное время всех фото.")]
        [SerializeField] private Image _closeProgress;

        [Header("Zoom")]
        [Tooltip("Целевая ширина фото при открытии (px). > 0 = все фото открываются до этого размера " +
                 "независимо от базового масштаба. 0 = использовать множитель Zoom Scale.")]
        [SerializeField] private float _zoomTargetWidth = 700f;
        [Tooltip("Множитель увеличения (используется, если Zoom Target Width = 0).")]
        [SerializeField] private float _zoomScale = 2f;
        [Tooltip("Время анимации зума (движение в центр + масштаб), сек.")]
        [SerializeField] private float _zoomDuration = 0.3f;
        [Tooltip("Сила пружины (overshoot) при зуме. 1 = без пружины, больше = сильнее отскок.")]
        [SerializeField] private float _zoomOvershoot = 1.7f;
        [Tooltip("Дополнительное смещение фото при зуме относительно его собственной позиции (0,0 = увеличивается на месте).")]
        [SerializeField] private Vector2 _zoomCenterOffset = Vector2.zero;

        [Header("Auto layout (только для настройки в редакторе)")]
        [Tooltip("Промежуток между фото при авто-раскладке.")]
        [SerializeField] private float _autoSpacing = 96f;
        [Tooltip("Вертикальная позиция ряда фото при авто-раскладке.")]
        [SerializeField] private float _autoY = 0f;

        public event Action CloseRequested;
        public event Action<bool> ZoomStateChanged;
        /// <summary>Клик: остановить авто-таймер и спрятать кольцо прогресса.</summary>
        public event Action TimerStopRequested;
        /// <summary>Клик: докрутить появление одной картинки (или показать следующую).</summary>
        public event Action SkipPhotoRequested;

        public IReadOnlyList<ComicPhoto> Photos => _photos;
        public float ZoomTargetWidth => _zoomTargetWidth;
        public float ZoomScale => _zoomScale;
        public Vector2 ZoomCenterOffset => _zoomCenterOffset;

        private readonly List<Vector3> _homeLocalPos = new();
        private readonly List<Vector3> _homeEuler = new();
        private readonly List<Vector3> _homeScale = new();
        private readonly List<int> _homeSibling = new();
        private readonly List<bool> _revealed = new();
        private readonly List<bool> _started = new();
        private int _zoomedIndex = -1;
        private bool _wired;
        private TMP_Text[] _texts;
        private string[] _textKeys;

        private void Awake()
        {
            LocalizationService.Instance.OnLanguageChanged += LanguageChangedHandler;
        }

        private void OnDestroy()
        {
            LocalizationService.Instance.OnLanguageChanged -= LanguageChangedHandler;
        }

        private void LanguageChangedHandler(string language) => LocalizeTexts();

        private void LocalizeTexts()
        {
            if (_texts == null)
            {
                _texts = GetComponentsInChildren<TMP_Text>(true);
                _textKeys = new string[_texts.Length];

                for (int i = 0; i < _texts.Length; i++)
                    _textKeys[i] = _texts[i] != null ? _texts[i].text : null;
            }

            for (int i = 0; i < _texts.Length; i++)
            {
                if (_texts[i] == null || string.IsNullOrEmpty(_textKeys[i])) continue;

                _texts[i].text = LocalizationService.Instance.GetText(_textKeys[i]);
            }
        }

        private void WireButtons()
        {
            if (_wired) return;
            _wired = true;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

            WireBackgroundClick();

            for (int i = 0; i < _photos.Count; i++)
            {
                var btn = _photos[i] != null ? _photos[i].Button : null;
                if (btn == null) continue;
                int index = i;
                btn.onClick.AddListener(() => OnPhotoClicked(index));
            }
        }

        private void WireBackgroundClick()
        {
            if (_darkBackground == null) return;

            _darkBackground.raycastTarget = true;

            var trigger = _darkBackground.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = _darkBackground.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ =>
            {
                // Клик в пустоту при открытом фото — сброс зума, не промотка.
                if (_zoomedIndex >= 0)
                {
                    ZoomOut();
                    return;
                }

                TimerStopRequested?.Invoke();
                SkipPhotoRequested?.Invoke();
            });
            trigger.triggers.Add(entry);
        }

        /// <summary>Готовит сцену к показу: затемнение вкл, все фото спрятаны (alpha 0), позиции запомнены.</summary>
        public void Prepare()
        {
            WireButtons();
            LocalizeTexts();

            if (_darkBackground != null)
                _darkBackground.gameObject.SetActive(true);

            _zoomedIndex = -1;

            _homeLocalPos.Clear();
            _homeEuler.Clear();
            _homeScale.Clear();
            _homeSibling.Clear();
            _revealed.Clear();
            _started.Clear();

            foreach (var photo in _photos)
            {
                if (photo == null)
                {
                    _homeLocalPos.Add(Vector3.zero);
                    _homeEuler.Add(Vector3.zero);
                    _homeScale.Add(Vector3.one);
                    _homeSibling.Add(0);
                    _revealed.Add(false);
                    _started.Add(false);
                    continue;
                }

                photo.gameObject.SetActive(true);
                photo.Group.alpha = 0f;
                photo.Group.blocksRaycasts = false;
                photo.Group.interactable = false;

                _homeLocalPos.Add(photo.Rect.localPosition);
                _homeEuler.Add(photo.Rect.localEulerAngles);
                _homeScale.Add(photo.Rect.localScale);
                _homeSibling.Add(photo.transform.GetSiblingIndex());
                _revealed.Add(false);
                _started.Add(false);
            }

            if (_closeProgress != null)
            {
                _closeProgress.gameObject.SetActive(true);
                _closeProgress.fillAmount = 0f;
            }
        }

        public bool IsPhotoRevealed(int index) =>
            index >= 0 && index < _revealed.Count && _revealed[index];

        public bool IsPhotoStarted(int index) =>
            index >= 0 && index < _started.Count && _started[index];

        /// <summary>Проявляет фото по индексу: fade-in только этого фото + pop-up (масштаб).</summary>
        public void ShowPhoto(int index)
        {
            if (index < 0 || index >= _photos.Count) return;
            var photo = _photos[index];
            if (photo == null) return;

            _started[index] = true;
            _revealed[index] = false;

            var group = photo.Group;
            var rt = photo.Rect;

            group.blocksRaycasts = true;
            group.interactable = true;
            group.DOKill();
            rt.DOKill();

            Vector3 home = index < _homeScale.Count ? _homeScale[index] : Vector3.one;
            int pending = 0;

            void OnPartComplete()
            {
                pending--;
                if (pending <= 0)
                    MarkRevealed(index);
            }

            if (photo.FadeDuration > 0f)
            {
                pending++;
                group.DOFade(1f, photo.FadeDuration).SetUpdate(true).OnComplete(OnPartComplete);
            }
            else
            {
                group.alpha = 1f;
            }

            if (photo.PopDuration > 0f)
            {
                pending++;
                rt.localScale = home * _popStartScale;
                rt.DOScale(home, photo.PopDuration).SetEase(Ease.OutBack).SetUpdate(true)
                    .OnComplete(OnPartComplete);
            }
            else
            {
                rt.localScale = home;
            }

            if (pending == 0)
                MarkRevealed(index);
        }

        /// <summary>Мгновенно докручивает появление фото до конца.</summary>
        public void CompletePhotoShow(int index)
        {
            if (index < 0 || index >= _photos.Count) return;
            var photo = _photos[index];
            if (photo == null) return;

            _started[index] = true;

            var group = photo.Group;
            var rt = photo.Rect;
            Vector3 home = index < _homeScale.Count ? _homeScale[index] : Vector3.one;

            group.DOKill();
            rt.DOKill();
            group.alpha = 1f;
            rt.localScale = home;
            group.blocksRaycasts = true;
            group.interactable = true;

            MarkRevealed(index);
        }

        public void SetCloseProgress(float normalized)
        {
            if (_closeProgress != null && _closeProgress.gameObject.activeSelf)
                _closeProgress.fillAmount = Mathf.Clamp01(normalized);
        }

        /// <summary>Прячет кольцо таймера, кнопку закрытия оставляет.</summary>
        public void HideCloseProgress()
        {
            if (_closeProgress != null)
                _closeProgress.gameObject.SetActive(false);
        }

        private void MarkRevealed(int index)
        {
            if (index < 0 || index >= _revealed.Count) return;
            _revealed[index] = true;
        }

        private void OnPhotoClicked(int index)
        {
            TimerStopRequested?.Invoke();

            // Ещё не появилось — клик = промотка появления, не зум.
            if (!IsPhotoRevealed(index))
            {
                SkipPhotoRequested?.Invoke();
                return;
            }

            if (_zoomedIndex >= 0)
            {
                if (index == _zoomedIndex)
                {
                    ZoomOut();
                    return;
                }

                // Клик по другому фото — сразу переключаем зум на него.
                ZoomOut();
            }

            ZoomIn(index);
        }

        private void ZoomIn(int index)
        {
            if (index < 0 || index >= _photos.Count) return;
            var photo = _photos[index];
            if (photo == null || !photo.gameObject.activeSelf) return;
            if (!IsPhotoRevealed(index)) return;

            var rt = photo.Rect;

            _zoomedIndex = index;
            ZoomStateChanged?.Invoke(true);

            rt.SetAsLastSibling();
            rt.DOKill();

            Vector3 home = index < _homeScale.Count ? _homeScale[index] : Vector3.one;

            float width = rt.sizeDelta.x;
            Vector3 targetScale = (_zoomTargetWidth > 0f && width > 0f)
                ? Vector3.one * (_zoomTargetWidth / width)
                : home * _zoomScale;

            var target = GetZoomedPosition(rt, index, targetScale);
            rt.DOLocalMove(target, _zoomDuration).SetUpdate(true);
            rt.DOScale(targetScale, _zoomDuration)
                .SetEase(Ease.OutBack, _zoomOvershoot).SetUpdate(true);
            rt.DOLocalRotate(Vector3.zero, _zoomDuration).SetUpdate(true);
        }

        /// <summary>
        /// Позиция фото при зуме: своё место (+ смещение), поджатое так,
        /// чтобы увеличенное фото не вылезло за пределы родителя.
        /// </summary>
        private Vector3 GetZoomedPosition(RectTransform rt, int index, Vector3 targetScale)
        {
            Vector3 home = index < _homeLocalPos.Count ? _homeLocalPos[index] : rt.localPosition;
            var target = new Vector3(home.x + _zoomCenterOffset.x, home.y + _zoomCenterOffset.y, home.z);

            if (rt.parent is RectTransform parent)
            {
                Rect bounds = parent.rect;
                float halfW = rt.rect.width * Mathf.Abs(targetScale.x) * 0.5f;
                float halfH = rt.rect.height * Mathf.Abs(targetScale.y) * 0.5f;

                // Если фото шире/выше родителя — центрируем по этой оси.
                target.x = halfW * 2f >= bounds.width
                    ? bounds.center.x
                    : Mathf.Clamp(target.x, bounds.xMin + halfW, bounds.xMax - halfW);
                target.y = halfH * 2f >= bounds.height
                    ? bounds.center.y
                    : Mathf.Clamp(target.y, bounds.yMin + halfH, bounds.yMax - halfH);
            }

            return target;
        }

        private void ZoomOut()
        {
            int index = _zoomedIndex;
            _zoomedIndex = -1;

            if (index < 0 || index >= _photos.Count || _photos[index] == null)
            {
                ZoomStateChanged?.Invoke(false);
                return;
            }

            var rt = _photos[index].Rect;
            Vector3 home = index < _homeScale.Count ? _homeScale[index] : Vector3.one;
            rt.DOKill();
            rt.DOLocalMove(_homeLocalPos[index], _zoomDuration).SetUpdate(true);
            rt.DOScale(home, _zoomDuration)
                .SetEase(Ease.OutBack, _zoomOvershoot).SetUpdate(true);
            rt.DOLocalRotate(_homeEuler[index], _zoomDuration).SetUpdate(true)
                .OnComplete(() => RestoreSibling(index));

            ZoomStateChanged?.Invoke(false);
        }

        private void RestoreSibling(int index)
        {
            if (index < 0 || index >= _photos.Count || _photos[index] == null) return;

            _photos[index].Rect.SetSiblingIndex(_homeSibling[index]);

            // Пока фото уезжало обратно, могло открыться другое — оно должно остаться сверху.
            if (_zoomedIndex >= 0 && _zoomedIndex < _photos.Count && _photos[_zoomedIndex] != null)
                _photos[_zoomedIndex].Rect.SetAsLastSibling();
        }

        public void Clear()
        {
            _zoomedIndex = -1;

            foreach (var photo in _photos)
            {
                if (photo == null) continue;
                photo.Rect.DOKill();
                photo.Group.DOKill();
                photo.Group.alpha = 0f;
            }

            if (_closeProgress != null)
            {
                _closeProgress.fillAmount = 0f;
                _closeProgress.gameObject.SetActive(true);
            }

            if (_darkBackground != null)
                _darkBackground.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Clear();
        }

#if UNITY_EDITOR
        [Button(ButtonSizes.Medium), PropertyOrder(-1)]
        [Tooltip("Разложить активные фото ровно и по центру. После можно двигать/крутить каждое вручную.")]
        private void ArrangeEvenly()
        {
            var active = new List<ComicPhoto>();
            foreach (var p in _photos)
                if (p != null) active.Add(p);

            int n = active.Count;
            if (n == 0) return;

            var center = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < n; i++)
            {
                var rt = active[i].Rect;
                float width = rt.sizeDelta.x;
                float step = width + _autoSpacing;
                float x = (i - (n - 1) / 2f) * step;
                rt.anchorMin = center;
                rt.anchorMax = center;
                rt.pivot = center;
                rt.anchoredPosition = new Vector2(x, _autoY);
                rt.localEulerAngles = Vector3.zero;
                UnityEditor.EditorUtility.SetDirty(rt);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
