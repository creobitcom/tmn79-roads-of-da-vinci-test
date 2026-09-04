using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Stages;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial
{
    [RequireComponent(typeof(RectTransform))]
    public class TutorialView : MonoBehaviour
    {
        public Button buttonToClose;
        public RectTransform mainRect;

        [SerializeField]
        [ReadOnly]
        [LabelText("Запомненное положение")]
        [InfoBox("Окно сдвинуто, но положение не запомнено — при показе в игре оно вернётся сюда. " +
                 "Нажми «Запомнить положение».", InfoMessageType.Warning, nameof(PositionOutOfSync))]
        private Vector2 position;

        /// <summary>
        /// Окно двигали в сцене, но не зафиксировали: SetPosition при показе вернёт его обратно.
        /// </summary>
        private bool PositionOutOfSync =>
            mainRect != null && (mainRect.anchoredPosition - position).sqrMagnitude > 0.01f;

        [FoldoutGroup("Анимации")]
        [LabelText("Появление")]
        [SerializeField]
        private TutorialAnimationSet showAnimation = new();

        [FoldoutGroup("Анимации")]
        [LabelText("Исчезновение")]
        [SerializeField]
        private TutorialAnimationSet hideAnimation = new();

        private TutorialAnimationPlayer.HomeState _home;
        private bool _homeCaptured;
        private CanvasGroup _canvasGroup;
        private bool _stageRaycastsEnabled = true;
        private Tween _tween;

        private RectTransform AnimationTarget => mainRect != null ? mainRect : (RectTransform)transform;

        private void OnEnable()
        {
            ResetStages(1);
        }

        [Button("Запомнить положение", ButtonSizes.Medium)]
        [GUIColor(0.5f, 0.9f, 0.5f)]
        public void GetPosition()
        {
            if (mainRect == null)
            {
                return;
            }

            position = mainRect.anchoredPosition;
        }

        public void SetPosition()
        {
            mainRect.anchoredPosition = position;
        }

        public UniTask PlayShow()
        {
            return PlayShowAtStage(1);
        }

        public UniTask PlayShowAtStage(int stage)
        {
            // Панель переиспользуется, поэтому остатки временного скрытия снимаем при каждом показе.
            IsHidden = false;
            ApplyHiddenState(false);

            PrepareAnimation();
            ResetStages(stage);

            return PlaySet(showAnimation, true);
        }

        public UniTask PlayHide()
        {
            PrepareAnimation();

            return PlaySet(hideAnimation, false);
        }

        #region Временное скрытие

        /// <summary>
        /// Окно спрятано «на время»: панель жива, стадия сохранена, но игроку окна не видно.
        /// </summary>
        public bool IsHidden { get; private set; }

        /// <summary>
        /// Спрятать окно, не закрывая туториал: играет обычную анимацию исчезновения, после
        /// которой окно гарантированно невидимо и не ловит клики. Панель не выгружается,
        /// поэтому её можно вернуть на той же стадии.
        /// </summary>
        public async UniTask PlayHideTemporary()
        {
            if (IsHidden)
            {
                return;
            }

            IsHidden = true;

            await PlayHide();

            // Пока играла анимация, окно могли вернуть — тогда гасить уже нечего.
            if (!IsHidden)
            {
                return;
            }

            // Анимации может не быть вовсе (старые окна) — тогда прячем напрямую,
            // иначе «скрытие» было бы незаметным.
            ApplyHiddenState(true);
        }

        /// <summary>
        /// Вернуть спрятанное окно с анимацией появления, НЕ сбрасывая стадию.
        /// </summary>
        public UniTask PlayShowKeepStage()
        {
            IsHidden = false;

            ApplyHiddenState(false);

            PrepareAnimation();

            return PlaySet(showAnimation, true);
        }

        /// <summary>
        /// Жёстко выставляет видимость через CanvasGroup — страховка на случай пустых анимаций.
        /// </summary>
        private void ApplyHiddenState(bool hidden)
        {
            var group = GetOrAddCanvasGroup();

            group.alpha = hidden ? 0f : 1f;
            group.blocksRaycasts = !hidden && _stageRaycastsEnabled;
            group.interactable = !hidden && _stageRaycastsEnabled;
        }

        public void SetWindowRaycastsEnabled(bool enabled)
        {
            _stageRaycastsEnabled = enabled;

            var group = GetOrAddCanvasGroup();
            group.blocksRaycasts = !IsHidden && enabled;
            group.interactable = !IsHidden && enabled;
        }

        private CanvasGroup GetOrAddCanvasGroup()
        {
            if (_canvasGroup != null)
            {
                return _canvasGroup;
            }

            var targetObject = AnimationTarget.gameObject;

            if (!targetObject.TryGetComponent(out _canvasGroup))
            {
                _canvasGroup = targetObject.AddComponent<CanvasGroup>();
            }

            return _canvasGroup;
        }

        #endregion

        #region Стадии

        [FoldoutGroup("Стадии")]
        [LabelText("Плавность перехода")]
        [SuffixLabel("сек", true)]
        [MinValue(0f)]
        [SerializeField]
        private float stageFadeDuration = 0.2f;

        [FoldoutGroup("Стадии")]
        [LabelText("Плавность по умолчанию")]
        [SerializeField]
        private Ease stageFadeEase = Ease.OutQuad;

        [FoldoutGroup("Стадии")]
        [LabelText("Поведение по стадиям")]
        [InfoBox("Пусто — вся стадия ведёт себя так, как настроено в SO окна. " +
                 "Добавь строку только для той стадии, где поведение отличается.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = false)]
        [SerializeField]
        private List<TutorialStageSettings> stageSettings = new();

        private TutorialStageElement[] _stageElements;
        private readonly Dictionary<TutorialStageElement, TutorialAnimationPlayer.HomeState> _stageHomes = new();
        private Tween _stageTween;

        /// <summary>
        /// Стадия сменилась. Контроллер по этому событию перечитывает флаги паузы и HUD.
        /// </summary>
        public event Action StageChanged;

        /// <summary>
        /// Стадия перестала быть текущей. Передаёт номер закрывшейся стадии.
        /// </summary>
        public event Action<int> StageHidden;

        /// <summary>
        /// Текущая стадия. Стадии считаются с 1, ноль — «видно всегда».
        /// </summary>
        public int CurrentStage { get; private set; }

        public int MaxStage
        {
            get
            {
                var max = 0;

                foreach (var element in StageElements)
                {
                    if (element != null)
                    {
                        max = Mathf.Max(max, element.Stage);
                    }
                }

                return max;
            }
        }

        public bool HasStages => MaxStage > 0;

        private TutorialStageElement[] StageElements =>
            _stageElements ??= GetComponentsInChildren<TutorialStageElement>(true);

        /// <summary>
        /// Пересобрать список элементов. Нужно после правок иерархии в редакторе.
        /// </summary>
        public void RefreshStages()
        {
            _stageElements = GetComponentsInChildren<TutorialStageElement>(true);
            _stageHomes.Clear();

            foreach (var element in _stageElements)
            {
                if (element != null && element.transform is RectTransform rect)
                {
                    _stageHomes[element] = TutorialAnimationPlayer.HomeState.Capture(rect);
                }
            }
        }

        /// <summary>
        /// Вернуть окно на первую стадию. Вызывается при каждом показе.
        /// </summary>
        public void ResetStages()
        {
            ResetStages(1);
        }

        public void ResetStages(int stage)
        {
            RefreshStages();

            CurrentStage = HasStages ? Mathf.Clamp(stage, 1, MaxStage) : 0;

            ApplyStage(CurrentStage, false);
        }

        public void GoNextStage() => GoToStage(CurrentStage + 1);

        public void GoPrevStage() => GoToStage(CurrentStage - 1);

        public void GoToStage(int stage)
        {
            var target = Mathf.Clamp(stage, HasStages ? 1 : 0, MaxStage);

            if (target == CurrentStage)
            {
                return;
            }

            var hiddenStage = CurrentStage;
            CurrentStage = target;

            // Если окно временно скрыто, сначала мгновенно собираем конечный вид новой стадии,
            // а затем контроллер показывает уже её. Иначе fade стадий идёт одновременно с
            // возвращением окна и старый шаг успевает мелькнуть перед новым.
            ApplyStage(CurrentStage, !IsHidden);

            StageChanged?.Invoke();
            StageHidden?.Invoke(hiddenStage);
        }

        #region Поведение стадии

        /// <summary>
        /// Итоговое значение флага: настройка стадии, а если там «как в окне» — значение из SO.
        /// </summary>
        public bool ResolvePauseLevelTimer(bool windowValue) =>
            TutorialStageSettings.Resolve(GetStageFlags()?.PauseLevelTimer ?? TutorialStageFlag.AsWindow,
                windowValue);

        public bool ResolveShowAboveHud(bool windowValue) =>
            TutorialStageSettings.Resolve(GetStageFlags()?.ShowAboveHud ?? TutorialStageFlag.AsWindow,
                windowValue);

        public bool ResolveBlockHudInput(bool windowValue) =>
            TutorialStageSettings.Resolve(GetStageFlags()?.BlockHudInput ?? TutorialStageFlag.AsWindow,
                windowValue);

        public bool ResolveUnskipTutorial(bool windowValue) =>
            TutorialStageSettings.Resolve(GetStageFlags()?.UnskipTutorial ?? TutorialStageFlag.AsWindow,
                windowValue);

        public bool IsStageUnskippable(int stage) =>
            GetStageFlags(stage)?.UnskipTutorial == TutorialStageFlag.On;

        public List<int> CollectUnskipStages()
        {
            var stages = new List<int>();

            if (stageSettings == null)
            {
                return stages;
            }

            foreach (var settings in stageSettings)
            {
                if (settings == null || settings.Stage <= 0
                    || settings.UnskipTutorial != TutorialStageFlag.On
                    || stages.Contains(settings.Stage))
                {
                    continue;
                }

                stages.Add(settings.Stage);
            }

            stages.Sort();

            return stages;
        }

        public bool ShouldPassClicksThroughWindow() => GetStageFlags()?.PassClicksThroughWindow ?? false;

        public TutorialStageSettings GetStageSettings(int stage) => GetStageFlags(stage);

        private TutorialStageSettings GetStageFlags(int? stage = null)
        {
            if (stageSettings == null)
            {
                return null;
            }

            foreach (var settings in stageSettings)
            {
                if (settings != null && settings.Stage == (stage ?? CurrentStage))
                {
                    return settings;
                }
            }

            return null;
        }

        #endregion

        /// <summary>
        /// Переходит на следующую стадию. Возвращает false, если текущая уже последняя —
        /// по этому признаку кнопка «Ок» понимает, что пора закрывать окно.
        /// </summary>
        public bool TryGoNextStage()
        {
            if (CurrentStage >= MaxStage)
            {
                return false;
            }

            GoToStage(CurrentStage + 1);

            return true;
        }

        private void ApplyStage(int stage, bool animated)
        {
            KillStageTween();

            var changes = new List<(TutorialStageElement Element, float From, float To)>();

            foreach (var element in StageElements)
            {
                if (element == null)
                {
                    continue;
                }

                var visible = element.IsVisibleOn(stage);
                var target = visible ? 1f : 0f;

                // Проходимость кликов переключаем сразу: пока элемент гаснет, ловить нажатия
                // он уже не должен.
                element.SetInteractive(visible);

                if (!Mathf.Approximately(element.Alpha, target))
                {
                    changes.Add((element, element.Alpha, target));
                }
            }

            if (changes.Count == 0)
            {
                return;
            }

            var settings = animated ? GetStageFlags(stage) : null;

            if (animated && settings?.Transition == TutorialStageTransition.Custom)
            {
                PlayCustomStageTransition(changes, settings.TransitionAnimation);
                return;
            }

            var duration = animated && settings?.Transition != TutorialStageTransition.Instant
                ? stageFadeDuration
                : 0f;

            if (duration <= 0f)
            {
                foreach (var change in changes)
                {
                    change.Element.SetAlpha(change.To);
                }

                return;
            }

            _stageTween = DOVirtual
                .Float(0f, 1f, duration, progress =>
                {
                    foreach (var change in changes)
                    {
                        if (change.Element != null)
                        {
                            change.Element.SetAlpha(Mathf.LerpUnclamped(change.From, change.To, progress));
                        }
                    }
                })
                .SetEase(stageFadeEase)
                .SetUpdate(true);
        }

        /// <summary>
        /// Проигрывает тот же набор Fade/Scale/Move/Rotate/Punch, что используется для окна,
        /// отдельно на каждом появляющемся и исчезающем элементе стадии.
        /// </summary>
        private void PlayCustomStageTransition(
            List<(TutorialStageElement Element, float From, float To)> changes,
            TutorialAnimationSet animation)
        {
            if (animation == null || animation.IsEmpty)
            {
                foreach (var change in changes)
                {
                    ResetStageElement(change.Element);
                    change.Element.SetAlpha(change.To);
                }

                return;
            }

            var sequence = DOTween.Sequence().SetUpdate(true);

            foreach (var change in changes)
            {
                var element = change.Element;

                if (element == null || element.transform is not RectTransform rect)
                {
                    continue;
                }

                ResetStageElement(element);

                // Без шага Fade элемент всё равно должен быть видим во время Move/Scale/etc.
                element.SetAlpha(1f);

                var tween = TutorialAnimationPlayer.Build(animation, rect, element.Group,
                    GetStageHome(element, rect), change.To > 0.5f);

                if (tween != null)
                {
                    sequence.Join(tween);
                }
            }

            sequence.OnComplete(() =>
            {
                foreach (var change in changes)
                {
                    if (change.Element == null)
                    {
                        continue;
                    }

                    ResetStageElement(change.Element);
                    change.Element.SetAlpha(change.To);
                }
            });

            _stageTween = sequence;
        }

        private TutorialAnimationPlayer.HomeState GetStageHome(TutorialStageElement element, RectTransform rect)
        {
            if (_stageHomes.TryGetValue(element, out var home))
            {
                return home;
            }

            home = TutorialAnimationPlayer.HomeState.Capture(rect);
            _stageHomes[element] = home;
            return home;
        }

        private void ResetStageElement(TutorialStageElement element)
        {
            if (element != null && element.transform is RectTransform rect)
            {
                TutorialAnimationPlayer.ResetToHome(rect, element.Group, GetStageHome(element, rect));
            }
        }

        private void KillStageTween()
        {
            if (_stageTween == null)
            {
                return;
            }

            _stageTween.Kill();
            _stageTween = null;
        }

        #endregion

        #region Предпросмотр в редакторе

        /// <summary>
        /// Длительность набора. Нужна редакторному превью, чтобы знать, сколько прокручивать.
        /// </summary>
        public float GetAnimationDuration(bool isShow) =>
            (isShow ? showAnimation : hideAnimation)?.TotalDuration ?? 0f;

        /// <summary>
        /// Готовит окно к покадровому предпросмотру: возвращает его в исходное состояние.
        /// </summary>
        public void PrepareAnimationPreview()
        {
            PrepareAnimation();
            EnsureCanvasGroup();
        }

        /// <summary>
        /// Показывает состояние анимации на момент <paramref name="time"/>.
        /// Считает тот же код, что и в игре, — поэтому превью совпадает с рантаймом.
        /// </summary>
        public void SampleAnimation(float time, bool isShow)
        {
            TutorialAnimationPlayer.Sample(isShow ? showAnimation : hideAnimation, time,
                AnimationTarget, _canvasGroup, _home, isShow);
        }

#if UNITY_EDITOR

        [FoldoutGroup("Стадии")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Сейчас показана")]
        private string EditorStageInfo => !HasStages
            ? "стадий нет"
            : CurrentStage <= 0
                ? $"0 — только общие элементы; в игре старт с 1 (всего {MaxStage})"
                : $"{CurrentStage} из {MaxStage}";

        [FoldoutGroup("Стадии")]
        [Button("◀ Предыдущая стадия", ButtonSizes.Medium)]
        private void EditorPrevStage() => ApplyStagePreview(CurrentStage - 1);

        [FoldoutGroup("Стадии")]
        [Button("Следующая стадия ▶", ButtonSizes.Medium)]
        private void EditorNextStage() => ApplyStagePreview(CurrentStage + 1);

        [FoldoutGroup("Стадии")]
        [Button("Показать все стадии", ButtonSizes.Medium)]
        [Tooltip("Снимает прозрачность со всех стадий — удобно перед сохранением в префаб.")]
        private void EditorShowAllStages()
        {
            RefreshStages();

            foreach (var element in StageElements)
            {
                if (element != null)
                {
                    element.SetAlpha(1f);
                    element.SetInteractive(true);
                }
            }
        }

#endif

        /// <summary>
        /// Мгновенно показывает нужную стадию. Для переключателя стадий в редакторе.
        /// </summary>
        public void ApplyStagePreview(int stage)
        {
            RefreshStages();

            CurrentStage = Mathf.Clamp(stage, 0, MaxStage);

            ApplyStage(CurrentStage, false);
        }

        /// <summary>
        /// Применяет пресет к обоим наборам. Используется тулзой создания туториалов.
        /// </summary>
        public void ApplyPreset(TutorialAnimationStep[] steps)
        {
            showAnimation.Set(showAnimation.DefaultDuration, steps);
            hideAnimation.Set(hideAnimation.DefaultDuration, steps);
        }

        #endregion

        private void PrepareAnimation()
        {
            KillTween();

            var target = AnimationTarget;

            // Домашнее состояние снимаем один раз — это вид окна на префабе. Дальше каждый показ
            // возвращает окно в него, чтобы остатки прошлого скрытия не накапливались.
            if (_homeCaptured)
            {
                TutorialAnimationPlayer.ResetToHome(target, _canvasGroup, _home);
                return;
            }

            _home = TutorialAnimationPlayer.HomeState.Capture(target);
            _homeCaptured = true;
        }

        private UniTask PlaySet(TutorialAnimationSet set, bool isShow)
        {
            if (set == null || set.IsEmpty)
            {
                return UniTask.CompletedTask;
            }

            _tween = TutorialAnimationPlayer.Build(set, AnimationTarget, EnsureCanvasGroup(), _home, isShow);

            return AwaitTween(_tween);
        }

        private static UniTask AwaitTween(Tween tween)
        {
            if (tween == null)
            {
                return UniTask.CompletedTask;
            }

            var completionSource = new UniTaskCompletionSource();

            tween.OnComplete(() => completionSource.TrySetResult());
            tween.OnKill(() => completionSource.TrySetResult());

            return completionSource.Task;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
            {
                return _canvasGroup;
            }

            var targetObject = AnimationTarget.gameObject;

            if (targetObject.TryGetComponent(out _canvasGroup))
            {
                return _canvasGroup;
            }

            // CanvasGroup добавляем только если он реально нужен — не хочется трогать префабы,
            // где прозрачность не анимируется.
            if (HasFade(showAnimation) || HasFade(hideAnimation))
            {
                _canvasGroup = targetObject.AddComponent<CanvasGroup>();
            }

            return _canvasGroup;
        }

        private static bool HasFade(TutorialAnimationSet set)
        {
            if (set == null || set.IsEmpty)
            {
                return false;
            }

            foreach (var step in set.Steps)
            {
                if (step.Kind == TutorialAnimationKind.Fade)
                {
                    return true;
                }
            }

            return false;
        }

        private void KillTween()
        {
            if (_tween == null)
            {
                return;
            }

            _tween.Kill();
            _tween = null;
        }

        private void OnDestroy()
        {
            KillTween();
            KillStageTween();
        }

        #region Пресеты анимаций

        [FoldoutGroup("Анимации")]
        [Button("Затухание", ButtonSizes.Medium)]
        private void PresetFade() => ApplyPreset(TutorialAnimationSet.PresetFade());

        [FoldoutGroup("Анимации")]
        [Button("Всплытие", ButtonSizes.Medium)]
        private void PresetGrow() => ApplyPreset(TutorialAnimationSet.PresetGrow());

        [FoldoutGroup("Анимации")]
        [Button("Снизу", ButtonSizes.Medium)]
        private void PresetFromBottom() =>
            ApplyPreset(TutorialAnimationSet.PresetFrom(TutorialAnimationDirection.Down));

        [FoldoutGroup("Анимации")]
        [Button("Сверху", ButtonSizes.Medium)]
        private void PresetFromTop() =>
            ApplyPreset(TutorialAnimationSet.PresetFrom(TutorialAnimationDirection.Up));

        [FoldoutGroup("Анимации")]
        [Button("С отскоком", ButtonSizes.Medium)]
        private void PresetBounce() => ApplyPreset(TutorialAnimationSet.PresetBounce());

        [FoldoutGroup("Анимации")]
        [Button("Без анимации", ButtonSizes.Medium)]
        private void PresetNone()
        {
            showAnimation.Clear();
            hideAnimation.Clear();
        }

        #endregion
    }
}
