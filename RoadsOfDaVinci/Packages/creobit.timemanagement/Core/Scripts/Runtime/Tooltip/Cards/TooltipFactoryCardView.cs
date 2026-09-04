using System.Collections.Generic;
using Flexalon;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Карточка тултипа фабрики: шапка + [текущий уровень] + стрелка + [стоимость апгрейда] + [следующий уровень].
    /// Блок следующего уровня прячется на максимальном уровне — тогда карточка честно
    /// показывает только текущее состояние, а не пустые слоты.
    /// </summary>
    public class TooltipFactoryCardView : MonoBehaviour, ITooltipCardView
    {
        [Title("Корень")]
        [SerializeField]
        [Tooltip("Flexalon-узел, обжимающийся под контент (Background). По нему считается размер тултипа " +
                 "для позиционирования над объектом.")]
        private FlexalonObject _rootFlexalon;

        [Title("Шапка")]
        [SerializeField]
        [Tooltip("Весь блок шапки. Прячется, если имя не задано.")]
        private GameObject _header;

        [SerializeField]
        [Tooltip("Подложка шапки. Спрайт меняется по состоянию 'хватает / не хватает'.")]
        private Image _headerBackground;

        [SerializeField]
        [Tooltip("Подложка, когда ресурсов на апгрейд хватает (marker_green).")]
        private Sprite _headerEnoughSprite;

        [SerializeField]
        [Tooltip("Подложка, когда не хватает (marker_red).")]
        private Sprite _headerNotEnoughSprite;

        [SerializeField]
        [Tooltip("Текст имени объекта. Шрифт AllodsWest.")]
        private TMP_Text _headerText;

        [SerializeField]
        [Tooltip("Flexalon-узел шапки. Её ширину код подгоняет под фон после того, как Flexalon его посчитал.")]
        private FlexalonObject _headerFlexalon;

        [SerializeField]
        [Tooltip("Отступ шапки от краёв фона с каждой стороны, px.")]
        private float _headerSideInset = 15f;

        [SerializeField]
        [Tooltip("МИНИМАЛЬНАЯ ширина шапки. К ней шапка сбрасывается перед каждым замером, иначе " +
                 "панель не может ужаться обратно после широкого тултипа. Под длинное имя шапка " +
                 "вырастет сама. Это же значение задаёт минимальную ширину всей панели: " +
                 "панель = max(шапка, тело) + отступы.")]
        private float _headerBaseWidth = 436f;

        [SerializeField]
        [Tooltip("Отступ текста от краёв шапки с каждой стороны, px. Должен совпадать с Padding " +
                 "Left/Right у Flexalon-узла шапки — по нему считается, при какой ширине текст " +
                 "перестаёт помещаться.")]
        private float _headerTextPadding = 12f;

        [SerializeField]
        [Tooltip("Цвет имени, когда хватает (#698713).")]
        private Color _headerEnoughColor = new(0.411765f, 0.529412f, 0.074510f, 1f);

        [SerializeField]
        [Tooltip("Цвет имени, когда не хватает (#BD290B).")]
        private Color _headerNotEnoughColor = new(0.741176f, 0.160784f, 0.043137f, 1f);

        [Title("Вёрстка (правится руками)")]
        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Сдвиг шапки по Y от позиции раскладки. RectTransform руками двигать бесполезно — " +
                 "Flexalon перезатрёт.")]
        private float _headerOffsetY;

        [SerializeField]
        [Tooltip("Flexalon-узел блока Body. Нужен для сдвига по Y.")]
        private FlexalonObject _bodyFlexalon;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Сдвиг блока Body по Y от позиции раскладки.")]
        private float _bodyOffsetY;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Вертикальный зазор между шапкой и блоком ячеек, px. В макете def.prefab — 6.")]
        private float _headerBodyGap = 6f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("С какой ширины начинается растяжение. Ширина панели из макета — 467.")]
        private float _minWidth = 467f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Минимальная высота панели. Из макета — 221.")]
        private float _minHeight = 221f;

        [Title("Текущий уровень")]
        [SerializeField]
        [Tooltip("Блок текущего уровня целиком.")]
        private GameObject _currentStageBlock;

        [SerializeField]
        [Tooltip("Метка 'Ур.N'. Шрифт AllodsWest.")]
        private TMP_Text _currentStageLabel;

        [SerializeField]
        [Tooltip("Ячейка таймера текущего уровня. Иконка часов остаётся из префаба.")]
        private TooltipCellView _currentStageTimer;

        [SerializeField]
        [Tooltip("Группа производимых ресурсов текущего уровня.")]
        private TooltipGroupView _currentStageOutput;

        [Title("Стрелка")]
        [SerializeField]
        [Tooltip("Стрелка текущий→следующий. Прячется на максимальном уровне.")]
        private GameObject _arrow;

        [Title("Стоимость апгрейда")]
        [SerializeField]
        [Tooltip("Блок требуемых ресурсов целиком.")]
        private GameObject _inputBlock;

        [SerializeField]
        [Tooltip("Группа требуемых на апгрейд ресурсов.")]
        private TooltipGroupView _inputGroup;

        [Title("Следующий уровень")]
        [SerializeField]
        [Tooltip("Блок следующего уровня целиком. Прячется на максимальном уровне.")]
        private GameObject _nextStageBlock;

        [SerializeField]
        [Tooltip("Метка 'Ур.N+1'. Шрифт AllodsWest.")]
        private TMP_Text _nextStageLabel;

        [SerializeField]
        [Tooltip("Ячейка таймера следующего уровня.")]
        private TooltipCellView _nextStageTimer;

        [SerializeField]
        [Tooltip("Группа производимых ресурсов следующего уровня.")]
        private TooltipGroupView _nextStageOutput;

        [Title("Локализация")]
        [SerializeField]
        [Tooltip("Ключ локализации метки уровня. В переводе должен быть плейсхолдер {0} — например 'Ур.{0}'. " +
                 "Если ключ не найден, покажем просто число.")]
        private string _levelKey = "game_txt_level_short";

        public Vector2 Size
        {
            get
            {
                var rect = _rootFlexalon != null
                    ? _rootFlexalon.transform as RectTransform
                    : transform as RectTransform;

                return rect == null ? Vector2.zero : new Vector2(rect.rect.width, rect.rect.height);
            }
        }

        public GameObject CurrentGameObject => gameObject;

        public void SetCardData(TooltipCardData data)
        {
            if (data == null)
            {
                return;
            }

            SetHeader(data.NameKey, data.NameSuffixKey, data.IsAffordable);

            SetStage(data.CurrentStage, _currentStageBlock, _currentStageLabel, _currentStageTimer, _currentStageOutput);
            SetStage(data.NextStage, _nextStageBlock, _nextStageLabel, _nextStageTimer, _nextStageOutput);

            SetInput(data.InputGroups);

            if (_arrow != null)
            {
                _arrow.SetActive(data.NextStage != null);
            }
        }

        private void SetHeader(string nameKey, string nameSuffixKey, bool isAffordable)
        {
            var hasName = !string.IsNullOrEmpty(nameKey);

            if (_header != null)
            {
                _header.SetActive(hasName);
            }

            if (!hasName)
            {
                return;
            }

            if (_headerBackground != null)
            {
                var sprite = isAffordable ? _headerEnoughSprite : _headerNotEnoughSprite;

                if (sprite != null)
                {
                    _headerBackground.sprite = sprite;
                }
            }

            if (_headerText != null)
            {
                _headerText.text = TooltipCardView.LocalizeHeader(nameKey, nameSuffixKey);
                _headerText.color = isAffordable ? _headerEnoughColor : _headerNotEnoughColor;
            }
        }

        private void SetStage(TooltipStageData stage, GameObject block, TMP_Text label,
            TooltipCellView timer, TooltipGroupView output)
        {
            if (block != null)
            {
                block.SetActive(stage != null);
            }

            if (stage == null)
            {
                return;
            }

            if (label != null)
            {
                label.text = FormatLevel(stage.DisplayLevel);
            }

            timer?.SetData(stage.Timer);

            if (output != null)
            {
                output.SetSeparatorVisible(false);
                output.SetData(stage.Output);
            }
        }

        private void SetInput(IReadOnlyList<TooltipGroupData> groups)
        {
            var hasInput = groups is { Count: > 0 } && groups[0].Cells.Count > 0;

            if (_inputBlock != null)
            {
                _inputBlock.SetActive(hasInput);
            }

            if (_inputGroup == null || !hasInput)
            {
                return;
            }

            _inputGroup.SetSeparatorVisible(false);
            _inputGroup.SetData(groups[0].Cells);
        }

        private string FormatLevel(int level)
        {
            var pattern = TooltipCardView.Localize(_levelKey);

            // Ключ не найден или в переводе забыли {0} — показываем хотя бы число,
            // а не строку "game_txt_level_short" на экране игрока.
            if (string.IsNullOrEmpty(pattern) || pattern == _levelKey || !pattern.Contains("{0}"))
            {
                return level.ToString();
            }

            return string.Format(pattern, level);
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }

        /// <summary>
        /// Из ITooltipView. Текст приходит УЖЕ локализованным — второй раз не локализуем.
        /// </summary>
        public void SetTooltipText(string objectName, string objectDescription)
        {
            if (_headerText != null && !string.IsNullOrEmpty(objectName))
            {
                _headerText.text = objectName;
            }
        }

        public void ForceUpdate()
        {
            if (_rootFlexalon == null)
            {
                return;
            }

            ApplyOffsets();
            MeasureAndFit();
        }

        /// <summary>
        /// Flexalon перезаписывает позиции детей на каждом пересчёте, поэтому правка
        /// RectTransform руками не живёт. Offset — живёт, на него и кладём поля из инспектора.
        /// </summary>
        private void ApplyOffsets()
        {
            // Зазор шапка↔тело живёт на раскладке фона.
            if (_rootFlexalon.TryGetComponent<FlexalonFlexibleLayout>(out var backgroundLayout)
                && backgroundLayout.isActiveAndEnabled)
            {
                backgroundLayout.Gap = _headerBodyGap;
            }

            // Проверка isActiveAndEnabled обязательна: у выключенного компонента Flexalon
            // не создал внутренний узел и его сеттеры падают NullReferenceException изнутри.
            if (_headerFlexalon != null && _headerFlexalon.isActiveAndEnabled)
            {
                _headerFlexalon.Offset = new Vector3(0f, _headerOffsetY, 0f);
            }

            if (_bodyFlexalon != null && _bodyFlexalon.isActiveAndEnabled)
            {
                _bodyFlexalon.Offset = new Vector3(0f, _bodyOffsetY, 0f);
            }
        }

        /// <summary>
        /// Размер панели считаем сами: померить контент → зажать по минимуму → зафиксировать
        /// и подтянуть шапку. MinWidth/MinHeight у Flexalon тут бесполезны: у узла с
        /// SizeType.Layout размер даёт замер по детям, и Min* его не ограничивает.
        ///
        /// Первый шаг обязательно сбрасывает тип размера и ширину шапки — их записали мы сами
        /// в прошлый показ, и без сброса они станут входом нового замера, а панель сможет
        /// только расти.
        /// </summary>
        private void MeasureAndFit()
        {
            _rootFlexalon.MinWidthType = MinMaxSizeType.None;
            _rootFlexalon.MinHeightType = MinMaxSizeType.None;
            _rootFlexalon.WidthType = SizeType.Layout;
            _rootFlexalon.HeightType = SizeType.Layout;
            ResetHeaderWidth();

            _rootFlexalon.ForceUpdate();

            if (_rootFlexalon.transform is not RectTransform background)
            {
                return;
            }

            var width = Mathf.Max(background.rect.width, _minWidth);
            var height = Mathf.Max(background.rect.height, _minHeight);

            // Сеттеры Width/Height сами переводят тип в Fixed.
            _rootFlexalon.Width = width;
            _rootFlexalon.Height = height;

            // Проверка isActiveAndEnabled обязательна: у выключенного компонента Flexalon
            // не создал внутренний узел и сеттер падает NullReferenceException изнутри.
            // Шапка выключается штатно — когда у объекта не задано имя.
            if (_headerFlexalon != null && _headerFlexalon.isActiveAndEnabled)
            {
                var headerWidth = width - _headerSideInset * 2f;

                if (headerWidth > 0f)
                {
                    _headerFlexalon.Width = headerWidth;
                }
            }

            _rootFlexalon.ForceUpdate();
        }

        /// <summary>
        /// Ширина шапки ПЕРЕД замером. Сброс обязателен: здесь лежит ширина, которую мы записали
        /// в прошлый показ, и без сброса она станет входом нового замера — панель сможет только
        /// расти и никогда не ужмётся обратно.
        ///
        /// Сбрасываем не в константу, а в максимум из базовой ширины и того, что просит текст.
        /// Шапка входит в замер фона обычным ребёнком с фиксированной шириной, поэтому панель
        /// умеет вырасти под длинное имя только так. А заодно Header Base Width становится
        /// настоящим минимумом панели: опусти его — и карточка на максимальном уровне,
        /// где нет ни стоимости, ни следующего блока, ужмётся до одного блока.
        /// </summary>
        private void ResetHeaderWidth()
        {
            if (_headerFlexalon == null || !_headerFlexalon.isActiveAndEnabled)
            {
                return;
            }

            var width = _headerBaseWidth;

            if (_headerText != null)
            {
                width = Mathf.Max(width, _headerText.preferredWidth + _headerTextPadding * 2f);
            }

            if (width > 0f)
            {
                _headerFlexalon.Width = width;
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [ButtonGroup("p1")]
        private void НеХватает() => Preview(Demo(false, true, 3));

        [ButtonGroup("p1")]
        private void Хватает() => Preview(Demo(true, true, 3));

        [ButtonGroup("p1")]
        private void МаксУровень() => Preview(Demo(true, false, 0));

        [ButtonGroup("p2")]
        private void Вход1() => Preview(Demo(false, true, 1));

        [ButtonGroup("p2")]
        private void Вход2() => Preview(Demo(false, true, 2));

        [ButtonGroup("p2")]
        private void Вход4() => Preview(Demo(false, true, 4));

        /// <summary>
        /// Обязательно с ForceUpdate. Голый SetCardData показывает НЕ то, что будет в игре:
        /// ширину панели и шапки считает MeasureAndFit, а Flexalon сам её не зовёт. Без этого
        /// шапка остаётся на базовой ширине из макета, панель уезжает шире неё — и в редакторе
        /// кажется, что шапка «не растягивается», хотя в рантайме всё сходится.
        /// </summary>
        private void Preview(TooltipCardData data)
        {
            SetCardData(data);
            ForceUpdate();
        }

        private static TooltipCardData Demo(bool affordable, bool hasNext, int inputs)
        {
            var data = new TooltipCardData
            {
                NameKey = "Фабрика",
                IsAffordable = affordable,
                CurrentStage = DemoStage(1),
            };

            if (hasNext)
            {
                data.NextStage = DemoStage(2);
            }

            if (inputs > 0)
            {
                var group = new TooltipGroupData { IsAffordable = affordable };

                for (var i = 0; i < inputs; i++)
                {
                    var state = affordable || i == 0 ? TooltipCellState.Enough : TooltipCellState.NotEnough;
                    group.Cells.Add(new TooltipCellData(null, ((i + 6) * 11).ToString(), state));
                }

                data.InputGroups.Add(group);
            }

            return data;
        }

        private static TooltipStageData DemoStage(int level)
        {
            var stage = new TooltipStageData
            {
                DisplayLevel = level,
                Timer = new TooltipCellData(null, "88", TooltipCellState.Neutral),
            };

            stage.Output.Add(new TooltipCellData(null, "99", TooltipCellState.Neutral));

            return stage;
        }
#endif
    }
}
