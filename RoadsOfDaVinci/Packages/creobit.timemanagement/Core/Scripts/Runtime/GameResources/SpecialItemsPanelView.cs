using System.Collections;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller;
using _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Data;
using DG.Tweening;
using Sirenix.OdinInspector;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    /// <summary>
    /// Альтернативная панель особых ресурсов: число видно всегда, сторона появления нового
    /// предмета настраивается, есть DOTween-анимации появления слота и прибавления счётчика.
    /// Опциональна: базовый <see cref="InventoryResourcesViewRefs"/> ведёт себя как раньше,
    /// поэтому проекты на нём (merge-логика) ничего не замечают.
    /// </summary>
    public class SpecialItemsPanelView : InventoryResourcesViewRefs
    {
        public enum InsertSide
        {
            Right,
            Left
        }

        [Title("Поведение")]
        [Tooltip("Показывать число даже когда предмет один.")]
        [SerializeField]
        private bool alwaysShowCount = true;

        [Tooltip("С какой стороны встаёт только что полученный предмет.")]
        [SerializeField]
        private InsertSide insertSide = InsertSide.Right;

        [Title("Анимация появления слота")]
        [SerializeField]
        private bool animateAppear = true;

        [Tooltip("Начальный масштаб слота, из которого он вырастает.")]
        [PropertyRange(0f, 1f)]
        [SerializeField]
        private float appearFromScale = 0.2f;

        [MinValue(0f)]
        [SerializeField]
        private float appearDuration = 0.35f;

        [SerializeField]
        private Ease appearEase = Ease.OutBack;

        [Title("Анимация прибавления счётчика")]
        [SerializeField]
        private bool animateCount = true;

        [Tooltip("Сила подскока кружка со счётчиком.")]
        [MinValue(0f)]
        [SerializeField]
        private float countPunchScale = 0.4f;

        [MinValue(0f)]
        [SerializeField]
        private float countPunchDuration = 0.3f;

        [Title("Тултип при наведении")]
        [Tooltip("Шаблон карточки для особых предметов. Пусто — тултипа не будет. " +
                 "Шаблон обязан лежать в Gameplay Settings → Tooltip Templates, иначе его карточка " +
                 "не предзагрузится и показ молча не сработает (в лог уйдёт ошибка).")]
        [SerializeField]
        private TooltipTemplate tooltipTemplate;

        [Tooltip("X — сдвиг карточки вбок, Y — зазор между низом слота и верхом карточки.")]
        [SerializeField]
        private Vector2 tooltipOffset = new(0f, 8f);

        [Title("Полёт предмета из мира")]
        [Tooltip("Скрафченный предмет прилетает из точки в мире в свой слот. Выключено — слот появляется сразу.")]
        [SerializeField]
        private bool deliverFromWorld = true;

        [Tooltip("Пусто — летящая иконка собирается в рантайме из спрайта самого предмета.")]
        [SerializeField]
        private Image flyIconPrefab;

        [Tooltip("Сдвиг точки вылета относительно переданного объекта, в мировых единицах.")]
        [SerializeField]
        private Vector3 flyWorldOffset = new(0f, 0.6f, 0f);

        [Tooltip("Во сколько раз иконка над объектом крупнее слота в панели.")]
        [MinValue(0f)]
        [SerializeField]
        private float flyStartScale = 1.6f;

        [MinValue(0f)]
        [SerializeField]
        private float flyPopDuration = 0.22f;

        [Tooltip("Пауза перед вылетом: игрок успевает разглядеть, что получил.")]
        [MinValue(0f)]
        [SerializeField]
        private float flyHangDuration = 0.18f;

        [MinValue(0f)]
        [SerializeField]
        private float flyDuration = 0.75f;

        [SerializeField]
        private Ease flyEase = Ease.InOutQuad;

        [Tooltip("Высота дуги над прямой «объект — слот», в пикселях канваса.")]
        [SerializeField]
        private float flyArcHeight = 120f;

        [Tooltip("Доворот иконки за полёт, в градусах.")]
        [SerializeField]
        private float flySpin = 12f;

        [MinValue(0f)]
        [SerializeField]
        private float flyLandDuration = 0.14f;

        [SerializeField]
        private bool flyTrail = true;

        [MinValue(0f)]
        [SerializeField]
        private float trailInterval = 0.05f;

        [PropertyRange(0f, 1f)]
        [SerializeField]
        private float trailAlpha = 0.45f;

        [MinValue(0f)]
        [SerializeField]
        private float trailFadeDuration = 0.3f;

        [Tooltip("Момент прилёта предмета в слот: сюда удобно вешать звук.")]
        [SerializeField]
        public UltEvent OnItemDelivered;

        private readonly HashSet<ResourceBaseSO> _incoming = new();

        private readonly List<RectTransform> _flyingIcons = new();

        // Последнее отрисованное количество для каждого слота: нужно, чтобы отличить
        // реальную прибавку от обычной перерисовки (она случается на любое изменение ресурсов).
        private readonly Dictionary<ResourceViewRefs, int> _shownAmounts = new();

        private ITooltipController _tooltipController;

        // Слот, по которому сейчас показан тултип. Нужен, чтобы гасить только СВОЙ показ:
        // курсор мог уйти с одного слота уже после того, как тултип перехватил соседний.
        private ResourceViewRefs _tooltipOwner;

        [Inject]
        private void Construct(ITooltipController tooltipController)
        {
            _tooltipController = tooltipController;
        }

        /// <summary>
        /// Показать карточку с именем предмета. Зовётся из <see cref="SpecialItemTooltipTrigger"/>
        /// на слоте — сам слот в контейнер не попадает, его создают обычным Instantiate.
        /// </summary>
        public void ShowItemTooltip(ResourceViewRefs slot)
        {
            if (_tooltipController == null || tooltipTemplate == null || slot == null || slot.resource == null)
            {
                return;
            }

            // Карточка предмета — только зелёная шапка с именем: ни входа, ни выхода, ни стрелки.
            // Вьюха гасит эти блоки сама, когда данных нет, поэтому отдельный класс не нужен.
            var data = new TooltipCardData
            {
                NameKey = slot.resource.Name,
                IsAffordable = true,
            };

            if (_tooltipController.ShowCardAtRect(tooltipTemplate, data, (RectTransform)slot.transform,
                    tooltipOffset))
            {
                _tooltipOwner = slot;
            }
        }

        public void HideItemTooltip(ResourceViewRefs slot)
        {
            if (_tooltipController == null || _tooltipOwner != slot)
            {
                return;
            }

            _tooltipOwner = null;

            // Пока курсор стоял на слоте, тултип мог перехватить объект уровня (на тач-удержании
            // мировой рейкаст идёт и поверх UI). Тогда гасить нечего — это уже чужой показ.
            if (_tooltipController.CurrentPrimaryTooltipObject != null)
            {
                return;
            }

            _tooltipController.HidePrimaryTooltip();
        }

        public override ResourceViewRefs GetResourceView()
        {
            var view = base.GetResourceView();

            if (insertSide == InsertSide.Left)
            {
                view.transform.SetAsFirstSibling();
            }

            // Слот мог прийти из пула из-под другого ресурса — его прошлое количество не наше.
            _shownAmounts.Remove(view);

            PlayAppear(view);

            return view;
        }

        public override void RemoveResource(ResourceViewRefs resourceView)
        {
            if (resourceView != null && resourceView.resource != null)
            {
                _incoming.Remove(resourceView.resource);
            }

            ResetTweens(resourceView);

            _shownAmounts.Remove(resourceView);

            base.RemoveResource(resourceView);
        }

        public override void SetResource(ResourceViewRefs resourceRefs, InventoryResource inventoryResource, int amount)
        {
            resourceRefs.resource = inventoryResource;
            resourceRefs.image.sprite = inventoryResource.Image;

            var showCount = alwaysShowCount ? amount > 0 : amount > 1;

            resourceRefs.countText.text = showCount ? amount.ToString() : string.Empty;

            if (resourceRefs.amountObject != null)
            {
                resourceRefs.amountObject.SetActive(showCount);
            }

            if (_shownAmounts.TryGetValue(resourceRefs, out var previousAmount) && amount > previousAmount
                && !_incoming.Contains(inventoryResource))
            {
                PlayCountPunch(resourceRefs);
            }

            _shownAmounts[resourceRefs] = amount;
        }

        private void PlayAppear(ResourceViewRefs view)
        {
            if (!animateAppear || !Application.isPlaying)
            {
                return;
            }

            var target = view.transform;

            target.DOKill();
            target.localScale = Vector3.one * appearFromScale;
            target.DOScale(Vector3.one, appearDuration).SetEase(appearEase);
        }

        private void PlayCountPunch(ResourceViewRefs view)
        {
            if (!animateCount || !Application.isPlaying || view.amountObject == null)
            {
                return;
            }

            var target = view.amountObject.transform;

            target.DOKill();
            target.localScale = Vector3.one;
            target.DOPunchScale(Vector3.one * countPunchScale, countPunchDuration);
        }

        // Слоты переиспользуются через пул, поэтому масштаб надо возвращать в единицу,
        // иначе следующий ресурс достанется недорисованным.
        private void ResetTweens(ResourceViewRefs view)
        {
            if (!Application.isPlaying || view == null)
            {
                return;
            }

            view.transform.DOKill();
            view.transform.localScale = Vector3.one;

            if (view.amountObject != null)
            {
                view.amountObject.transform.DOKill();
                view.amountObject.transform.localScale = Vector3.one;
            }
        }

        public bool PlayItemDelivery(InventoryResource resource, Vector3 worldPosition, Camera worldCamera = null)
        {
            if (!deliverFromWorld || !Application.isPlaying || resource == null)
            {
                return false;
            }

            var slot = FindSlot(resource);

            if (slot == null)
            {
                return false;
            }

            var canvas = GetComponentInParent<Canvas>();
            var camera = worldCamera != null ? worldCamera : Camera.main;

            if (canvas == null || camera == null)
            {
                return false;
            }

            var canvasRect = (RectTransform)canvas.transform;
            var screenPoint = camera.WorldToScreenPoint(worldPosition + flyWorldOffset);
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            if (screenPoint.z < 0f
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera,
                    out var startPoint))
            {
                return false;
            }

            var isNewSlot = !_shownAmounts.TryGetValue(slot, out var shownAmount) || shownAmount <= 1;

            HoldSlotUntilDelivered(slot, resource, isNewSlot);

            if (resourcesParent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }

            var targetRect = slot.image != null ? slot.image.rectTransform : (RectTransform)slot.transform;
            var endPoint = (Vector2)canvasRect.InverseTransformPoint(targetRect.position);
            var icon = CreateFlyIcon(canvasRect, resource, targetRect.rect.size);

            icon.anchoredPosition = startPoint;

            PlayFlight(icon, startPoint, endPoint, slot, resource, isNewSlot);

            return true;
        }

        private ResourceViewRefs FindSlot(InventoryResource resource)
        {
            foreach (var view in resourceTextImages)
            {
                if (view != null && view.resource == resource)
                {
                    return view;
                }
            }

            return null;
        }

        private void HoldSlotUntilDelivered(ResourceViewRefs slot, InventoryResource resource, bool hideSlot)
        {
            _incoming.Add(resource);

            ResetTweens(slot);

            if (hideSlot)
            {
                slot.transform.localScale = Vector3.zero;
            }
        }

        private RectTransform CreateFlyIcon(RectTransform canvasRect, InventoryResource resource, Vector2 size)
        {
            Image image;

            if (flyIconPrefab != null)
            {
                image = Instantiate(flyIconPrefab, canvasRect);
            }
            else
            {
                var iconObject = new GameObject($"FlyingItem_{resource.Name}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));

                iconObject.transform.SetParent(canvasRect, false);

                image = iconObject.GetComponent<Image>();
            }

            image.sprite = resource.Image;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var rect = image.rectTransform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (flyStartScale * 0.3f);
            rect.SetAsLastSibling();

            if (rect.GetComponent<CanvasGroup>() == null)
            {
                rect.gameObject.AddComponent<CanvasGroup>();
            }

            _flyingIcons.Add(rect);

            return rect;
        }

        private void PlayFlight(RectTransform icon, Vector2 startPoint, Vector2 endPoint, ResourceViewRefs slot,
            InventoryResource resource, bool growSlot)
        {
            var group = icon.GetComponent<CanvasGroup>();
            var middle = (startPoint + endPoint) * 0.5f + Vector2.up * flyArcHeight;

            group.alpha = 0f;

            var flight = DOTween.Sequence();

            flight.Append(icon.DOScale(flyStartScale, flyPopDuration).SetEase(Ease.OutBack));
            flight.Join(group.DOFade(1f, flyPopDuration * 0.6f));
            flight.AppendInterval(flyHangDuration);
            flight.Append(icon
                .DOLocalPath(new[] { (Vector3)middle, (Vector3)endPoint }, flyDuration, PathType.CatmullRom)
                .SetEase(flyEase));
            flight.Join(icon.DOScale(1f, flyDuration).SetEase(Ease.InQuad));

            if (!Mathf.Approximately(flySpin, 0f))
            {
                flight.Join(icon.DOLocalRotate(new Vector3(0f, 0f, flySpin), flyDuration).SetEase(Ease.InOutSine));
            }

            flight.AppendCallback(() => LandSlot(slot, resource, growSlot));
            flight.Append(icon.DOScale(0.45f, flyLandDuration).SetEase(Ease.InQuad));
            flight.Join(group.DOFade(0f, flyLandDuration));
            flight.OnComplete(() => CompleteDelivery(icon));
            flight.SetLink(icon.gameObject);

            if (flyTrail)
            {
                StartCoroutine(SpawnTrail(icon, flight));
            }
        }

        private IEnumerator SpawnTrail(RectTransform icon, Sequence flight)
        {
            yield return new WaitForSeconds(flyPopDuration + flyHangDuration);

            var wait = new WaitForSeconds(trailInterval);

            while (icon != null && flight != null && flight.IsActive() && flight.IsPlaying())
            {
                SpawnGhost(icon);

                yield return wait;
            }
        }

        private void SpawnGhost(RectTransform icon)
        {
            var source = icon.GetComponent<Image>();

            if (source == null || source.sprite == null)
            {
                return;
            }

            var ghostObject = new GameObject("FlyingItemGhost", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));

            var ghost = (RectTransform)ghostObject.transform;

            ghost.SetParent(icon.parent, false);
            ghost.SetSiblingIndex(icon.GetSiblingIndex());
            ghost.anchorMin = icon.anchorMin;
            ghost.anchorMax = icon.anchorMax;
            ghost.pivot = icon.pivot;
            ghost.sizeDelta = icon.sizeDelta;
            ghost.anchoredPosition = icon.anchoredPosition;
            ghost.localRotation = icon.localRotation;
            ghost.localScale = icon.localScale;

            var ghostImage = ghostObject.GetComponent<Image>();

            ghostImage.sprite = source.sprite;
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;

            var ghostGroup = ghostObject.GetComponent<CanvasGroup>();

            ghostGroup.alpha = trailAlpha;
            ghostGroup.DOFade(0f, trailFadeDuration).SetEase(Ease.OutQuad).SetLink(ghostObject);

            ghost.DOScale(ghost.localScale * 0.7f, trailFadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(ghostObject)
                .OnComplete(() => Destroy(ghostObject));
        }

        private void LandSlot(ResourceViewRefs slot, InventoryResource resource, bool growSlot)
        {
            _incoming.Remove(resource);

            if (slot == null || slot.resource != resource || !slot.gameObject.activeInHierarchy)
            {
                return;
            }

            if (growSlot)
            {
                PlayAppear(slot);
            }

            if (_shownAmounts.TryGetValue(slot, out var amount) && amount > 1)
            {
                PlayCountPunch(slot);
            }

            OnItemDelivered?.Invoke();
        }

        private void CompleteDelivery(RectTransform icon)
        {
            _flyingIcons.Remove(icon);

            if (icon != null)
            {
                Destroy(icon.gameObject);
            }
        }

        private void OnDisable()
        {
            _incoming.Clear();

            for (var index = _flyingIcons.Count - 1; index >= 0; index--)
            {
                var icon = _flyingIcons[index];

                if (icon == null)
                {
                    continue;
                }

                icon.DOKill();

                Destroy(icon.gameObject);
            }

            _flyingIcons.Clear();

            foreach (var view in resourceTextImages)
            {
                if (view != null)
                {
                    view.transform.localScale = Vector3.one;
                }
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [InfoBox("Превью показывает раскладку: слоты помечены несохраняемыми и в префаб не попадут. " +
                 "Если не видно ничего — панель не лежит под Canvas.")]
        [ButtonGroup("preview")]
        private void Предметов1() => ShowPreview(1);

        [ButtonGroup("preview")]
        private void Предметов2() => ShowPreview(2);

        [ButtonGroup("preview")]
        private void Предметов3() => ShowPreview(3);

        [ButtonGroup("preview")]
        private void Предметов4() => ShowPreview(4);

        [Button("Очистить превью")]
        private void ОчиститьПревью() => ClearPreview();

        private void ShowPreview(int count)
        {
            // В Play Mode слоты создаёт GameResourcesSystem: фейковые перемешались бы с настоящими.
            if (Application.isPlaying)
            {
                Debug.LogWarning($"{nameof(SpecialItemsPanelView)}: превью доступно только вне Play Mode.", this);

                return;
            }

            ClearPreview();

            if (prefab == null || resourcesParent == null)
            {
                Debug.LogWarning($"{nameof(SpecialItemsPanelView)}: не заданы prefab или resourcesParent.", this);

                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"{nameof(SpecialItemsPanelView)}: корень панели выключен, превью не будет видно — "
                                 + "включи галочку у GameObject.", this);
            }

            // Без Canvas в предке ни один Graphic не рисуется: слоты создадутся, но экран будет пустым.
            if (GetComponentInParent<Canvas>() == null)
            {
                Debug.LogWarning($"{nameof(SpecialItemsPanelView)}: над панелью нет Canvas — слоты создадутся, "
                                 + "но отрисованы не будут.", this);
            }

            for (var i = 0; i < count; i++)
            {
                var view = Instantiate(prefab, resourcesParent);
                var amount = i + 1;

                view.gameObject.name = $"[preview] {amount}";

                foreach (var child in view.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.hideFlags = HideFlags.DontSave;
                }

                var showCount = alwaysShowCount ? amount > 0 : amount > 1;

                if (view.countText != null)
                {
                    view.countText.text = showCount ? amount.ToString() : string.Empty;
                }

                if (view.amountObject != null)
                {
                    view.amountObject.SetActive(showCount);
                }
            }
        }

        private void ClearPreview()
        {
            if (resourcesParent == null)
            {
                return;
            }

            for (var i = resourcesParent.childCount - 1; i >= 0; i--)
            {
                var child = resourcesParent.GetChild(i).gameObject;

                if (child.hideFlags == HideFlags.DontSave)
                {
                    DestroyImmediate(child);
                }
            }
        }
#endif
    }
}
