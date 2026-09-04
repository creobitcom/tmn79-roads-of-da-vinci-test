using _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations;
using DG.Tweening;
using Flexalon;
using FlexalonRuntime = Flexalon.Flexalon;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    [DisallowMultipleComponent]
    public class TooltipCardAnimator : MonoBehaviour
    {
        [Title("Цель")]
        [SerializeField]
        [Tooltip("Что анимируем. Пусто — корень карточки. Узлы внутри Flexalon-раскладки сюда " +
                 "указывать нельзя: раскладка задаёт им позицию и размер сама.")]
        private RectTransform _animationTarget;

        [Title("Анимации")]
        [SerializeField]
        [Tooltip("Появление карточки. Пустой набор — карточка появляется мгновенно, как раньше.")]
        private TutorialAnimationSet _showAnimation = new();

        [SerializeField]
        [Tooltip("Исчезновение карточки. Пустой набор — карточка гаснет мгновенно, как раньше.")]
        private TutorialAnimationSet _hideAnimation = new();

        private readonly FrozenTransformUpdater _frozenTransform = new();

        private TutorialAnimationPlayer.HomeState _home;
        private bool _homeCaptured;
        private CanvasGroup _canvasGroup;
        private FlexalonNode _node;
        private bool _nodeResolved;
        private bool _transformFrozen;
        private Tween _tween;
        private bool _isHiding;

        public bool IsHiding => _isHiding;

        private RectTransform Target => _animationTarget != null ? _animationTarget : transform as RectTransform;

        public void PrepareShow()
        {
            KillTween();

            _isHiding = false;

            ResetToHome();
        }

        public void PlayShow()
        {
            var target = Target;

            if (target == null)
            {
                return;
            }

            _home = TutorialAnimationPlayer.HomeState.Capture(target);
            _homeCaptured = true;

            if (_showAnimation == null || _showAnimation.IsEmpty)
            {
                return;
            }

            SetTransformFrozen(true);

            _tween = TutorialAnimationPlayer.Build(_showAnimation, target, EnsureCanvasGroup(), _home, true);

            if (_tween == null)
            {
                SetTransformFrozen(false);

                return;
            }

            _tween.OnComplete(FinishShow);
        }

        public bool PlayHide()
        {
            if (_isHiding)
            {
                return true;
            }

            var target = Target;

            if (target == null || !gameObject.activeSelf || _hideAnimation == null || _hideAnimation.IsEmpty)
            {
                return false;
            }

            KillTween();

            if (_homeCaptured)
            {
                ResetToHome();
            }
            else
            {
                _home = TutorialAnimationPlayer.HomeState.Capture(target);
                _homeCaptured = true;
            }

            SetTransformFrozen(true);

            _tween = TutorialAnimationPlayer.Build(_hideAnimation, target, EnsureCanvasGroup(), _home, false);

            if (_tween == null)
            {
                SetTransformFrozen(false);

                return false;
            }

            _isHiding = true;

            _tween.OnComplete(FinishHide);

            return true;
        }

        public void RebaseHome()
        {
            if (!_homeCaptured)
            {
                return;
            }

            var target = Target;

            if (target == null)
            {
                return;
            }

            _home = new TutorialAnimationPlayer.HomeState(target.anchoredPosition, _home.LocalScale,
                _home.LocalEulerAngles);
        }

        public void Cancel()
        {
            KillTween();

            _isHiding = false;

            ResetToHome();
        }

        private void OnDisable()
        {
            KillTween();

            _isHiding = false;

            ResetToHome();
        }

        private void FinishShow()
        {
            _tween = null;

            SetTransformFrozen(false);
        }

        private void FinishHide()
        {
            _tween = null;
            _isHiding = false;

            SetTransformFrozen(false);

            ResetToHome();

            gameObject.SetActive(false);
        }

        private void ResetToHome()
        {
            if (!_homeCaptured)
            {
                return;
            }

            TutorialAnimationPlayer.ResetToHome(Target, _canvasGroup, _home);
        }

        private void KillTween()
        {
            SetTransformFrozen(false);

            if (_tween == null)
            {
                return;
            }

            if (_tween.IsActive())
            {
                _tween.Kill();
            }

            _tween = null;
        }

        private CanvasGroup EnsureCanvasGroup()
        {
            if (_canvasGroup != null)
            {
                return _canvasGroup;
            }

            var target = Target;

            if (target == null)
            {
                return null;
            }

            if (target.TryGetComponent(out _canvasGroup))
            {
                return _canvasGroup;
            }

            if (HasFade(_showAnimation) || HasFade(_hideAnimation))
            {
                _canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
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

        private void SetTransformFrozen(bool frozen)
        {
            if (_transformFrozen == frozen)
            {
                return;
            }

            var node = GetFlexalonNode();

            if (node == null)
            {
                return;
            }

            _transformFrozen = frozen;

            node.SetTransformUpdater(frozen ? _frozenTransform : null);

            if (!frozen)
            {
                node.MarkDirty();
            }
        }

        private FlexalonNode GetFlexalonNode()
        {
            if (_nodeResolved)
            {
                return _node;
            }

            _nodeResolved = true;

            var target = Target;

            if (target != null && target.TryGetComponent<FlexalonObject>(out _))
            {
                _node = FlexalonRuntime.GetOrCreateNode(target.gameObject);
            }

            return _node;
        }

        private class FrozenTransformUpdater : TransformUpdater
        {
            public void PreUpdate(FlexalonNode node)
            {
            }

            public bool UpdatePosition(FlexalonNode node, Vector3 position) => true;

            public bool UpdateRotation(FlexalonNode node, Quaternion rotation) => true;

            public bool UpdateScale(FlexalonNode node, Vector3 scale) => true;

            public bool UpdateRectSize(FlexalonNode node, Vector2 size)
            {
                if (node.GameObject.transform is not RectTransform rectTransform)
                {
                    return true;
                }

                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

                return true;
            }
        }
    }
}
