using System;
using System.Collections.Generic;
using System.Threading;
using _8floor.TimeManagement.Core.Scripts.Runtime.Utils;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.UI
{
    public class ObjectAlternativeSelectionView : MonoBehaviour
    {
        [SerializeField] private ObjectAlternativeSelectionButton _buttonPrefab;
        [SerializeField] private Transform _buttonsContainer;

        [Header("Global UI Settings (Для ГД)")]
        [SerializeField] private Vector2 _globalOffset = new Vector2(0, 150f);

        [SerializeField] private float _globalScale = 1f;

        [Header("Settings")]
        [SerializeField] private float _autoHideTime = 4f;
        [SerializeField] private Button _backgroundCloseButton;

        /// <summary>
        /// Кнопки живут между показами и переиспользуются. Раньше каждый показ уничтожал их
        /// через DestroyImmediate и создавал заново; теперь панель ещё и пересобирается на лету
        /// (изменился набор доступных альтернатив), и пересоздание отдавало бы мусор каждые
        /// несколько кадров.
        /// </summary>
        private readonly List<ObjectAlternativeSelectionButton> _buttonPool = new();

        private readonly List<ObjectAlternativeSelectionButton> _activeButtons = new();
        private readonly List<int> _shownIndices = new();

        private CancellationTokenSource _hideCts;

        private event Action _onCancelCallback;
        private event Action<int> _onSelectCallback;

        /// <summary>
        /// Предикат доступности, с которым панель показали. Держим его, чтобы перепроверять
        /// доступность на лету: он считает ресурсы, свободных юнитов и проходимость пути
        /// в момент вызова, а не в момент показа.
        /// </summary>
        private Func<int, bool> _canAffordPredicate;

        private ObjectView _objectView;

        /// <summary>
        /// Взводится в <see cref="Show"/> перед активацией объекта. Отличает честный показ
        /// от панели, случайно сохранённой активной в сцене или префабе.
        /// </summary>
        private bool _isShownByCode;

        private bool _autoHideEnabled = true;

        private int _onlyAllowedAlternative = -1;

        /// <summary>Панель на экране. По ней снаружи решают, есть ли что обновлять.</summary>
        public bool IsShown => gameObject.activeSelf;

        /// <summary>Объект, для которого открыта панель. null — панель не показана.</summary>
        public ObjectView Owner => IsShown ? _objectView : null;

        public int ShownAlternativesCount => _shownIndices.Count;

        private void Awake()
        {
            if (_backgroundCloseButton != null)
            {
                _backgroundCloseButton.onClick.AddListener(OnBackgroundClicked);
            }

            // Панель показывается только через Show(). Если она сохранена в сцене/префабе
            // активной, то с момента загрузки её невидимый полноэкранный BackgroundBlocker висит
            // поверх остального UI и молча съедает первый клик игрока (закрываясь об него) —
            // например, клик по "нажмите, чтобы начать" на старте уровня.
            if (!_isShownByCode)
            {
                gameObject.SetActive(false);
            }
        }

        public void Show(
            ObjectView objectView,
            List<int> availableIndices,
            Func<int, bool> canAfford,
            Action<int> onSelect,
            Action onCancel,
            Camera mainCamera,
            Canvas mainCanvas)
        {
            // Панель одна на сцену. Если её показывают заново, пока она уже висит (клик по
            // другому объекту), старый показ надо честно закрыть: иначе выбор, сделанный для
            // прошлого объекта, так и останется висеть в ActiveAlternativeIndex.
            if (gameObject.activeSelf)
            {
                CancelTimer();
                _onCancelCallback?.Invoke();
            }

            // До SetActive: при самой первой активации Awake выполняется прямо внутри SetActive(true),
            // и без взведённого флага он тут же погасил бы панель.
            _isShownByCode = true;

            gameObject.SetActive(true);

            _objectView = objectView;
            _onCancelCallback = onCancel;
            _onSelectCallback = onSelect;
            _canAffordPredicate = canAfford;

            float finalScale = objectView.overrideAlternativeUIScale
                ? objectView.alternativeUIScaleOverride
                : _globalScale;
            _buttonsContainer.localScale = new Vector3(finalScale, finalScale, 1f);

            Vector2 finalOffset = _globalOffset + objectView.alternativeUIOffsetOverride;

            var rect = (RectTransform)_buttonsContainer;
            var calculatedPosition = UIHelper.ConvertWorldToLocalCanvasPosition(
                objectView.BadgePosition,
                mainCamera,
                mainCanvas,
                Vector2.up,
                finalOffset,
                rect.rect.width * finalScale,
                rect.rect.height * finalScale);

            _buttonsContainer.localPosition = calculatedPosition;

            BuildButtons(availableIndices);

            if (_autoHideEnabled)
            {
                StartAutoHideTimer().Forget();
            }
        }

        public void SetAllowedAlternative(int index)
        {
            _onlyAllowedAlternative = index;

            ApplyAlternativeLock();
        }

        public void SetAutoHideEnabled(bool value)
        {
            _autoHideEnabled = value;

            if (!value)
            {
                CancelTimer();
                return;
            }

            if (IsShown && _objectView != null)
            {
                StartAutoHideTimer().Forget();
            }
        }

        /// <summary>
        /// Пересобрать кнопки под новый набор доступных альтернатив, не закрывая панель
        /// и НЕ трогая таймер авто-скрытия: игрок ничего не выбрал, отсчёт продолжается с того же
        /// места. Раньше на изменение набора панель просто закрывалась через <see cref="Close"/>,
        /// причём по завершению взаимодействия ЛЮБОГО объекта на уровне.
        /// </summary>
        public void Rebuild(IReadOnlyList<int> availableIndices)
        {
            if (!IsShown || _objectView == null)
            {
                return;
            }

            BuildButtons(availableIndices);
        }

        /// <summary>
        /// Совпадает ли показанный набор кнопок с переданным. Нужно, чтобы отличать
        /// «поменялся состав вариантов» (пересобрать кнопки) от «поменялась только доступность»
        /// (перекрасить иконки) — второе намного дешевле и не дёргает layout.
        /// </summary>
        public bool HasSameIndices(IReadOnlyList<int> indices)
        {
            if (indices == null || indices.Count != _shownIndices.Count)
            {
                return false;
            }

            for (var i = 0; i < indices.Count; i++)
            {
                if (indices[i] != _shownIndices[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Закрыть панель снаружи, как будто игрок кликнул мимо. Зовётся, когда вариантов
        /// не осталось вовсе или объект стал недоступен.
        ///
        /// НЕ зовётся на «где-то на уровне что-то изменилось»: раньше именно так и было —
        /// панель гасил OnEndInteract любого объекта, — и панель закрывалась под руками
        /// у игрока. Такие случаи теперь идут через <see cref="Rebuild"/> и
        /// <see cref="RefreshAffordability"/>.
        /// </summary>
        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            HideAndCancel();
        }

        /// <summary>
        /// Пересчитать серые/обычные иконки у уже показанных кнопок.
        /// Зовётся, когда изменилось что угодно из того, от чего зависит доступность: ресурсы,
        /// занятость юнитов, проходимость пути. Предикат считает всё это заново — панель при этом
        /// не закрывается и не мигает.
        /// Состав кнопок не трогаем: за него отвечает <see cref="Rebuild"/>.
        /// </summary>
        public void RefreshAffordability()
        {
            if (!gameObject.activeSelf || _canAffordPredicate == null)
            {
                return;
            }

            foreach (var btn in _activeButtons)
            {
                if (btn == null) continue;

                btn.SetAffordable(_canAffordPredicate(btn.Index));
            }
        }

        private void BuildButtons(IReadOnlyList<int> availableIndices)
        {
            DeactivateButtons();

            _shownIndices.Clear();

            if (availableIndices == null)
            {
                return;
            }

            var alternatives = _objectView.ObjectDataSO.AlternativeInteractions;

            for (var i = 0; i < availableIndices.Count; i++)
            {
                var index = availableIndices[i];

                if (index < 0 || index >= alternatives.Count)
                {
                    continue;
                }

                var alternative = alternatives[index];
                var btn = GetButton(i);

                btn.Initialize(
                    alternative.AlternativeIcon,
                    alternative.AlternativeIconDisabled,
                    _canAffordPredicate == null || _canAffordPredicate(index),
                    index,
                    OnAlternativeSelected);

                _activeButtons.Add(btn);
                _shownIndices.Add(index);
            }

            ApplyAlternativeLock();
        }

        private void ApplyAlternativeLock()
        {
            foreach (var btn in _activeButtons)
            {
                if (btn == null)
                {
                    continue;
                }

                btn.SetInteractionBlocked(_onlyAllowedAlternative >= 0 && btn.Index != _onlyAllowedAlternative);
            }
        }

        /// <summary>
        /// Кнопка по порядковому месту: из пула, либо новая. SetSiblingIndex — чтобы порядок
        /// кнопок на экране совпадал с порядком альтернатив после пересборки.
        /// </summary>
        private ObjectAlternativeSelectionButton GetButton(int slot)
        {
            while (_buttonPool.Count <= slot)
            {
                _buttonPool.Add(Instantiate(_buttonPrefab, _buttonsContainer));
            }

            var btn = _buttonPool[slot];

            btn.transform.SetSiblingIndex(slot);
            btn.gameObject.SetActive(true);

            return btn;
        }

        private void DeactivateButtons()
        {
            foreach (var btn in _buttonPool)
            {
                if (btn != null) btn.gameObject.SetActive(false);
            }

            _activeButtons.Clear();
        }

        private void OnBackgroundClicked()
        {
            if (!_autoHideEnabled)
            {
                return;
            }

            HideAndCancel();
        }

        private void OnAlternativeSelected(int selectedIndex)
        {
            CancelTimer();
            _canAffordPredicate = null;
            _objectView = null;
            var onSelect = _onSelectCallback;
            _onSelectCallback = null;
            _onCancelCallback = null;
            onSelect?.Invoke(selectedIndex);
            gameObject.SetActive(false);
        }

        private void HideAndCancel()
        {
            CancelTimer();
            var cancel = _onCancelCallback;
            _canAffordPredicate = null;
            _objectView = null;
            _onCancelCallback = null;
            _onSelectCallback = null;
            var wasActive = gameObject.activeSelf;
            gameObject.SetActive(false);
            if (wasActive)
            {
                cancel?.Invoke();
            }
        }

        private async UniTaskVoid StartAutoHideTimer()
        {
            CancelTimer();
            _hideCts = new CancellationTokenSource();

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_autoHideTime), cancellationToken: _hideCts.Token);
                HideAndCancel();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelTimer()
        {
            if (_hideCts == null) return;

            _hideCts.Cancel();
            _hideCts.Dispose();
            _hideCts = null;
        }

        private void OnDestroy()
        {
            CancelTimer();

            if (_backgroundCloseButton != null)
            {
                _backgroundCloseButton.onClick.RemoveListener(OnBackgroundClicked);
            }

            _onCancelCallback = null;
            _onSelectCallback = null;
            _canAffordPredicate = null;
            _objectView = null;
        }

        private void OnValidate()
        {
            if (_buttonsContainer != null)
            {
                ApplyEditorSettings();
            }
        }

        private void ApplyEditorSettings()
        {
            if (_buttonsContainer != null)
            {
                _buttonsContainer.localScale = new Vector3(_globalScale, _globalScale, 1f);
                _buttonsContainer.localPosition = new Vector3(_globalOffset.x, _globalOffset.y, 0f);
            }
        }

        [GUIColor(0f, 1f, 0f)]
        [Button("TEST: Сгенерировать 2 кнопки (Для настройки)", ButtonSizes.Medium)]
        private void TestGenerateTwoButtons()
        {
            if (_buttonPrefab == null || _buttonsContainer == null)
            {
                Debug.LogError("Назначь Button Prefab и Buttons Container!");
                return;
            }

            TestClearButtons();

            for (int i = 0; i < 2; i++)
            {
                var btn = Instantiate(_buttonPrefab, _buttonsContainer);
                btn.Initialize(null, i, null);
                _activeButtons.Add(btn);
                _buttonPool.Add(btn);
            }

            ApplyEditorSettings();
        }

        [GUIColor(1f, 0.4f, 0f)]
        [Button("TEST: Удалить тестовые кнопки", ButtonSizes.Medium)]
        private void TestClearButtons()
        {
            _activeButtons.Clear();
            _buttonPool.Clear();
            _shownIndices.Clear();

            if (_buttonsContainer == null)
            {
                return;
            }

            for (var i = _buttonsContainer.childCount - 1; i >= 0; i--)
            {
                var child = _buttonsContainer.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
