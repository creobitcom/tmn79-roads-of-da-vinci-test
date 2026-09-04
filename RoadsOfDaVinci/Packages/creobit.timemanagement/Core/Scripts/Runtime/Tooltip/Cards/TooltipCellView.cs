using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Одна ячейка тултипа: иконка + число + красный круг "не хватает".
    /// Универсальная — ею рисуется и требуемый ресурс, и получаемый, и таймер фабрики.
    /// Цвета и круг ставит код (как в RecipeResourceView крафта), а не UltEvent'ы префаба.
    /// </summary>
    public class TooltipCellView : MonoBehaviour
    {
        [Title("Ссылки")]
        [SerializeField]
        [Tooltip("Иконка ресурса. Спрайт подставляет код. Для таймера — иконка часов из префаба.")]
        private Image _icon;

        [SerializeField]
        [Tooltip("Число. Шрифт Alkatra. Цвет ставит код по состоянию.")]
        private TMP_Text _amount;

        [SerializeField]
        [Tooltip("Красный круг 'не хватает'. Включается кодом. По умолчанию выключен.")]
        private GameObject _notEnoughCircle;

        [Title("Цвета числа")]
        [SerializeField]
        [Tooltip("Ресурса хватает (#698713).")]
        private Color _enoughColor = new(0.411765f, 0.529412f, 0.074510f, 1f);

        [SerializeField]
        [Tooltip("Ресурса не хватает (#ED3A3A).")]
        private Color _notEnoughColor = new(0.929412f, 0.227451f, 0.227451f, 1f);

        [SerializeField]
        [Tooltip("Нейтральная ячейка — выход и таймер (#5D2D16).")]
        private Color _neutralColor = new(0.364706f, 0.176471f, 0.086275f, 1f);

        /// <summary>
        /// Заполнить ячейку. Иконка null (таймер) — спрайт из префаба остаётся как есть.
        /// </summary>
        public void SetData(TooltipCellData data)
        {
            if (_icon != null && data.Icon != null)
            {
                _icon.sprite = data.Icon;
                _icon.enabled = true;
            }

            if (_amount != null)
            {
                _amount.text = data.Text ?? string.Empty;
                _amount.color = GetColor(data.State);
            }

            if (_notEnoughCircle != null)
            {
                _notEnoughCircle.SetActive(data.State == TooltipCellState.NotEnough);
            }
        }

        private Color GetColor(TooltipCellState state)
        {
            switch (state)
            {
                case TooltipCellState.Enough: return _enoughColor;
                case TooltipCellState.NotEnough: return _notEnoughColor;
                default: return _neutralColor;
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [ButtonGroup("preview")]
        private void Хватает() => SetData(new TooltipCellData(null, "66", TooltipCellState.Enough));

        [ButtonGroup("preview")]
        private void НеХватает() => SetData(new TooltipCellData(null, "77", TooltipCellState.NotEnough));

        [ButtonGroup("preview")]
        private void Нейтральная() => SetData(new TooltipCellData(null, "88", TooltipCellState.Neutral));
#endif
    }
}
