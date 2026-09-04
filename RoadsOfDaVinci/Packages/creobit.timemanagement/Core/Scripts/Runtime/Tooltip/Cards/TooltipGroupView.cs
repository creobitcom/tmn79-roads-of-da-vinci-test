using System.Collections.Generic;
using Creobit.Logger;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Группа ячеек тултипа. Ячейки ПРЕД-РАЗМЕЩЕНЫ в префабе внутри layout-контейнера:
    /// код только включает нужное количество, раскладку и ширину считает сам layout.
    /// Так превью работает в редакторе, а верстальщик правит spacing руками.
    ///
    /// Разделитель ("oder") живёт внутри группы и включается у всех групп кроме последней,
    /// поэтому альтернатив может быть сколько угодно, а не ровно две.
    /// </summary>
    public class TooltipGroupView : MonoBehaviour
    {
        [Title("Ячейки (пред-размещены)")]
        [SerializeField]
        [Tooltip("Слоты ячеек. Порядок важен — код заполняет слева направо. " +
                 "Нужно больше ресурсов в группе — добавь ячейку в префаб, код менять не надо.")]
        private TooltipCellView[] _cells;

        [Title("Разделитель")]
        [SerializeField]
        [Tooltip("Метка 'oder'. Включается кодом у всех групп кроме последней. По умолчанию выключена.")]
        private GameObject _separator;

        /// <summary>Сколько ячеек физически есть в префабе.</summary>
        public int Capacity => _cells?.Length ?? 0;

        /// <summary>
        /// Расстояние между ячейками. Ставит карточка из своего поля в инспекторе.
        ///
        /// Проверка isActiveAndEnabled обязательна: у выключенного компонента Flexalon не создал
        /// внутренний узел, и его сеттер падает NullReferenceException изнутри.
        /// Выключенные группы (вторая альтернатива) — штатное состояние, а не ошибка.
        /// </summary>
        public void SetGap(float gap)
        {
            if (!TryGetComponent<Flexalon.FlexalonFlexibleLayout>(out var layout))
            {
                return;
            }

            if (!layout.isActiveAndEnabled)
            {
                return;
            }

            layout.Gap = gap;
        }

        /// <summary>
        /// Показать n ячеек и заполнить их. Лишние данные не теряются молча — пишем в лог,
        /// потому что тихо обрезанный список читается как "всё влезло".
        /// </summary>
        public void SetData(IReadOnlyList<TooltipCellData> cells)
        {
            var count = cells?.Count ?? 0;

            if (_cells == null || _cells.Length == 0)
            {
                return;
            }

            if (count > _cells.Length)
            {
                Log.Gameplay.Warning(
                    $"TooltipGroupView '{name}': ячеек в префабе {_cells.Length}, а ресурсов {count}. " +
                    "Лишние не показаны — добавь слоты в префаб.");
            }

            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == null)
                {
                    continue;
                }

                var active = i < count;
                _cells[i].gameObject.SetActive(active);

                if (active)
                {
                    _cells[i].SetData(cells[i]);
                }
            }
        }

        /// <summary>
        /// Отступы вокруг "oder". Слева от него по умолчанию стоит зазор между ячейками,
        /// справа — зазор между группами: они разные, поэтому перекос лечится этими полями.
        /// </summary>
        public void SetSeparatorMargins(float left, float right)
        {
            if (_separator == null || !_separator.activeInHierarchy)
            {
                return;
            }

            if (!_separator.TryGetComponent<Flexalon.FlexalonObject>(out var flex) || !flex.isActiveAndEnabled)
            {
                return;
            }

            flex.MarginLeft = left;
            flex.MarginRight = right;
        }

        /// <summary>Показать/скрыть "oder" после этой группы.</summary>
        public void SetSeparatorVisible(bool visible)
        {
            if (_separator != null)
            {
                _separator.SetActive(visible);
            }
        }

        /// <summary>Текст разделителя (локализованный) — подставляет карточка.</summary>
        public void SetSeparatorText(string text)
        {
            if (_separator == null)
            {
                return;
            }

            var label = _separator.GetComponentInChildren<TMPro.TMP_Text>(true);

            if (label != null && !string.IsNullOrEmpty(text))
            {
                label.text = text;
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [ButtonGroup("preview")]
        private void Ячеек1() => PreviewCells(1);

        [ButtonGroup("preview")]
        private void Ячеек2() => PreviewCells(2);

        [ButtonGroup("preview")]
        private void Ячеек3() => PreviewCells(3);

        [ButtonGroup("preview")]
        private void Ячеек4() => PreviewCells(4);

        [ButtonGroup("preview2")]
        private void РазделительВкл() => SetSeparatorVisible(true);

        [ButtonGroup("preview2")]
        private void РазделительВыкл() => SetSeparatorVisible(false);

        private void PreviewCells(int count)
        {
            var demo = new List<TooltipCellData>();
            var states = new[] { TooltipCellState.Enough, TooltipCellState.NotEnough, TooltipCellState.NotEnough, TooltipCellState.Enough };

            for (var i = 0; i < count; i++)
            {
                demo.Add(new TooltipCellData(null, ((i + 6) * 11).ToString(), states[i % states.Length]));
            }

            SetData(demo);
        }
#endif
    }
}
