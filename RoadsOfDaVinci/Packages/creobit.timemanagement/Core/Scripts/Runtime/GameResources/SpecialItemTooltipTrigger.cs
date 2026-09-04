using UnityEngine;
using UnityEngine.EventSystems;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameResources
{
    /// <summary>
    /// Наведение на слот особого предмета → карточка тултипа с именем предмета.
    /// Висит на префабе слота рядом с <see cref="ResourceViewRefs"/>.
    ///
    /// Панель ищется вверх по иерархии, а не инжектится: слоты создаёт
    /// <see cref="InventoryResourcesViewRefs.GetResourceView"/> обычным Instantiate,
    /// мимо контейнера, поэтому [Inject] на них не сработал бы.
    ///
    /// Тач отдельной ветки не требует: EventSystem шлёт OnPointerEnter на нажатии
    /// и OnPointerExit на отпускании, то есть удержание пальцем работает само —
    /// так же, как мировые тултипы на Hold-интеракции.
    /// </summary>
    [RequireComponent(typeof(ResourceViewRefs))]
    public class SpecialItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ResourceViewRefs _refs;
        private SpecialItemsPanelView _panel;
        private bool _isHovered;

        private void Awake()
        {
            _refs = GetComponent<ResourceViewRefs>();

            // includeInactive: панель в сцене стартует выключенной и включается только
            // на первом предмете (OnInventoryShow).
            _panel = GetComponentInParent<SpecialItemsPanelView>(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_panel == null || _refs == null)
            {
                return;
            }

            _isHovered = true;

            _panel.ShowItemTooltip(_refs);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hide();
        }

        /// <summary>
        /// Слот уходит в пул (выключается), когда предмет потратили. OnPointerExit при этом
        /// НЕ приходит — без этого тултип остался бы висеть над пустым местом.
        /// </summary>
        private void OnDisable()
        {
            // При выгрузке сцены OnDisable прилетает всем подряд в неопределённом порядке,
            // и карточки тултипа могут быть уже уничтожены — прятать в этот момент нечего,
            // а обращение к ним даст MissingReferenceException. Гасим только на живой сцене.
            if (!gameObject.scene.isLoaded)
            {
                _isHovered = false;

                return;
            }

            Hide();
        }

        private void Hide()
        {
            if (!_isHovered)
            {
                return;
            }

            _isHovered = false;

            if (_panel != null)
            {
                _panel.HideItemTooltip(_refs);
            }
        }
    }
}
