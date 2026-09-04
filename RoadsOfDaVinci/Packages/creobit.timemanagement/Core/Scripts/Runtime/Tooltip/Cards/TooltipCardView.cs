using System.Collections.Generic;
using Creobit.Localization;
using Creobit.Logger;
using Flexalon;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Cards
{
    /// <summary>
    /// Обычная карточка тултипа LA8: шапка + вход + стрелка + выход.
    ///
    /// Покрывает сразу четыре макета, потому что все секции опциональны:
    ///   вход+выход · только вход · только выход · альтернативы ("oder").
    /// Альтернативы — это просто несколько групп входа, разделитель рисует сама группа.
    /// </summary>
    public class TooltipCardView : MonoBehaviour, ITooltipCardView
    {
        [Title("Корень")]
        [SerializeField]
        [Tooltip("Flexalon-узел, обжимающийся под контент (Background). По нему считается размер тултипа " +
                 "для позиционирования над объектом. Тот же принцип, что в старом TooltipView.")]
        private FlexalonObject _rootFlexalon;

        [Title("Шапка")]
        [SerializeField]
        [Tooltip("Весь блок шапки. Прячется, если имя не задано.")]
        private GameObject _header;

        [SerializeField]
        [Tooltip("Подложка шапки. Спрайт меняется по состоянию 'хватает / не хватает'.")]
        private Image _headerBackground;

        [SerializeField]
        [Tooltip("Подложка, когда ресурсов хватает (marker_green).")]
        private Sprite _headerEnoughSprite;

        [SerializeField]
        [Tooltip("Подложка, когда ресурсов не хватает (marker_red).")]
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
        [Tooltip("Базовая ширина шапки из макета. К ней шапка сбрасывается перед каждым замером, " +
                 "иначе панель не может ужаться обратно после широкого тултипа.")]
        private float _headerBaseWidth = 307f;

        [SerializeField]
        [Tooltip("Цвет имени, когда хватает (#698713).")]
        private Color _headerEnoughColor = new(0.411765f, 0.529412f, 0.074510f, 1f);

        [SerializeField]
        [Tooltip("Цвет имени, когда не хватает (#BD290B).")]
        private Color _headerNotEnoughColor = new(0.741176f, 0.160784f, 0.043137f, 1f);

        [Title("Вёрстка (правится руками)")]
        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Сдвиг шапки по Y от позиции, которую посчитал Flexalon. Двигать RectTransform руками " +
                 "бесполезно — раскладка его перезатрёт; крути это поле.")]
        private float _headerOffsetY;

        [SerializeField]
        [Tooltip("Flexalon-узел блока Body (вход+стрелка+выход). Нужен для сдвига по Y.")]
        private FlexalonObject _bodyFlexalon;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Сдвиг блока Body по Y от позиции раскладки.")]
        private float _bodyOffsetY;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Ширина панели по умолчанию. Панель НИКОГДА не уже этого значения; " +
                 "шире становится, только если содержимое не влезло. Из макета — 337.")]
        private float _minWidth = 337f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Высота панели по умолчанию. Панель никогда не ниже этого значения. Из макета — 180.")]
        private float _minHeight = 180f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Ширина красной/зелёной плашки шапки = ширина панели минус это значение с каждой стороны.")]
        private float _headerInsetFromPanel = 15f;

        [SerializeField]
        [Tooltip("Flexalon-узел стрелки. Нужен для отступов слева/справа от неё.")]
        private FlexalonObject _arrowFlexalon;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Отступ слева от стрелки, px.")]
        private float _arrowGapLeft = 5f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Отступ справа от стрелки, px.")]
        private float _arrowGapRight = 5f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Вертикальный зазор между шапкой и блоком ячеек, px. В макете def.prefab — 6.")]
        private float _headerBodyGap = 6f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Расстояние между карточками ресурсов внутри группы, px.")]
        private float _cellGap = 2f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Отступ СЛЕВА от 'oder', px. Складывается с Cell Gap — разделитель лежит внутри группы.")]
        private float _separatorGapLeft;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Отступ СПРАВА от 'oder', px. Складывается с Group Gap.")]
        private float _separatorGapRight;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Зазор между группами альтернатив, px. Это он даёт отступ справа от 'oder'.")]
        private float _groupGap = 8f;

        [SerializeField]
        [OnValueChanged(nameof(ForceUpdate))]
        [Tooltip("Отступы содержимого от краёв панели: X — слева, Y — справа, Z — сверху, W — снизу.")]
        private Vector4 _contentPadding = new(15f, 15f, 12f, 14f);

        [Title("Вход")]
        [SerializeField]
        [Tooltip("Блок входа целиком. Прячется, если требуемых ресурсов нет.")]
        private GameObject _inputBlock;

        [SerializeField]
        [Tooltip("Группы входа. Одна группа = обычный тултип, несколько = альтернативы через 'oder'. " +
                 "Нужно больше альтернатив — добавь группу в префаб, код менять не надо.")]
        private TooltipGroupView[] _inputGroups;

        [Title("Стрелка")]
        [SerializeField]
        [Tooltip("Стрелка вход→выход. Показывается только когда есть И вход, И выход.")]
        private GameObject _arrow;

        [Title("Превью (рамка сбоку)")]
        [SerializeField]
        [Tooltip("Блок рамки с картинкой того, что даст действие. Прячется, если картинки нет. " +
                 "Лежит СНАРУЖИ корневого Flexalon-узла, поэтому в размер карточки не входит.")]
        private GameObject _previewBlock;

        [SerializeField]
        [Tooltip("Image внутри рамки, куда подставляется картинка.")]
        private Image _previewImage;

        [Title("Выход")]
        [SerializeField]
        [Tooltip("Блок выхода целиком. Прячется, если получаемых ресурсов нет.")]
        private GameObject _outputBlock;

        [SerializeField]
        [Tooltip("Группа получаемых ресурсов. Разделитель у неё всегда выключен.")]
        private TooltipGroupView _outputGroup;

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
            SetPreview(data.PreviewIcon);
            SetInputGroups(data.InputGroups);
            SetOutput(data.Output);

            var hasInput = data.InputGroups is { Count: > 0 };
            var hasOutput = data.Output is { Count: > 0 };

            if (_arrow != null)
            {
                _arrow.SetActive(hasInput && hasOutput);
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
                _headerText.text = LocalizeHeader(nameKey, nameSuffixKey);
                _headerText.color = isAffordable ? _headerEnoughColor : _headerNotEnoughColor;
            }
        }

        /// <summary>
        /// Картинка «что дадут» в рамке сбоку. Нет картинки — прячем рамку целиком:
        /// префаб один на все раскопки, а превью есть не у каждого объекта.
        /// </summary>
        private void SetPreview(Sprite icon)
        {
            if (_previewBlock != null)
            {
                _previewBlock.SetActive(icon != null);
            }

            if (_previewImage != null && icon != null)
            {
                _previewImage.sprite = icon;
            }
        }

        private void SetInputGroups(IReadOnlyList<TooltipGroupData> groups)
        {
            var count = groups?.Count ?? 0;

            if (_inputBlock != null)
            {
                _inputBlock.SetActive(count > 0);
            }

            if (_inputGroups == null || _inputGroups.Length == 0)
            {
                return;
            }

            if (count > _inputGroups.Length)
            {
                Log.Gameplay.Warning(
                    $"TooltipCardView '{name}': групп входа в префабе {_inputGroups.Length}, а альтернатив {count}. " +
                    "Лишние не показаны — добавь группы в префаб.");
            }

            var separatorText = Localize(SeparatorKey);

            for (var i = 0; i < _inputGroups.Length; i++)
            {
                if (_inputGroups[i] == null)
                {
                    continue;
                }

                var active = i < count;
                _inputGroups[i].gameObject.SetActive(active);

                if (!active)
                {
                    continue;
                }

                _inputGroups[i].SetData(groups[i].Cells);

                // "oder" рисуем у всех групп кроме последней показанной
                var isLast = i == count - 1;
                _inputGroups[i].SetSeparatorVisible(!isLast);

                // Если локализация не поднята (редакторское превью), GetText вернёт сам ключ —
                // не подставляем его, оставляем текст из префаба.
                if (!isLast && separatorText != SeparatorKey)
                {
                    _inputGroups[i].SetSeparatorText(separatorText);
                }
            }
        }

        private void SetOutput(IReadOnlyList<TooltipCellData> output)
        {
            var count = output?.Count ?? 0;

            if (_outputBlock != null)
            {
                _outputBlock.SetActive(count > 0);
            }

            if (_outputGroup == null)
            {
                return;
            }

            _outputGroup.SetSeparatorVisible(false);

            if (count > 0)
            {
                _outputGroup.SetData(output);
            }
        }

        public void SetPosition(Vector2 position)
        {
            transform.localPosition = position;
        }

        /// <summary>
        /// Из ITooltipView. Текст приходит УЖЕ локализованным, поэтому второй раз не локализуем —
        /// иначе получится GetText(GetText(key)) и на экран уйдёт мусор.
        /// Новый макет описания не показывает, поэтому используется только имя.
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
            // Flexalon, а не LayoutRebuilder: раскладку тултипов в проекте считает он,
            // и вложенные ContentSizeFitter'ы (которые тут были) при ребилде схлопывают карточку.
            if (_rootFlexalon == null)
            {
                return;
            }

            ApplyTuning();
            MeasureAndFit();
        }

        /// <summary>
        /// Всё, что правится в инспекторе, применяем на каждом пересчёте: Flexalon перезаписывает
        /// позиции и размеры детей, поэтому ручная правка RectTransform не живёт.
        /// </summary>
        private void ApplyTuning()
        {
            // Зазор шапка↔тело живёт на раскладке фона.
            if (_rootFlexalon.TryGetComponent<FlexalonFlexibleLayout>(out var backgroundLayout)
                && backgroundLayout.isActiveAndEnabled)
            {
                backgroundLayout.Gap = _headerBodyGap;
            }

            if (_headerFlexalon != null && _headerFlexalon.isActiveAndEnabled)
            {
                _headerFlexalon.Offset = new Vector3(0f, _headerOffsetY, 0f);
            }

            if (_bodyFlexalon != null && _bodyFlexalon.isActiveAndEnabled)
            {
                _bodyFlexalon.Offset = new Vector3(0f, _bodyOffsetY, 0f);
            }

            if (_arrowFlexalon != null && _arrowFlexalon.isActiveAndEnabled)
            {
                _arrowFlexalon.MarginLeft = _arrowGapLeft;
                _arrowFlexalon.MarginRight = _arrowGapRight;
            }

            _rootFlexalon.PaddingLeft = _contentPadding.x;
            _rootFlexalon.PaddingRight = _contentPadding.y;
            _rootFlexalon.PaddingTop = _contentPadding.z;
            _rootFlexalon.PaddingBottom = _contentPadding.w;

            // Зазор между группами — он же отступ справа от "oder".
            if (_inputBlock != null
                && _inputBlock.TryGetComponent<FlexalonFlexibleLayout>(out var inputLayout)
                && inputLayout.isActiveAndEnabled)
            {
                inputLayout.Gap = _groupGap;
            }

            if (_inputGroups != null)
            {
                foreach (var group in _inputGroups)
                {
                    // Выключенные группы пропускаем: у них нет живого Flexalon-узла.
                    if (group != null && group.isActiveAndEnabled)
                    {
                        group.SetGap(_cellGap);
                        group.SetSeparatorMargins(_separatorGapLeft, _separatorGapRight);
                    }
                }
            }

            if (_outputGroup != null && _outputGroup.isActiveAndEnabled)
            {
                _outputGroup.SetGap(_cellGap);
            }
        }

        /// <summary>
        /// Размер панели считаем САМИ и ставим жёстко; Flexalon только расставляет детей внутри.
        ///
        /// Почему так: у узла с SizeType.Layout размер даёт замер по детям, и MinWidth/MinHeight
        /// его не ограничивают — на них полагаться нельзя, поле просто не работало.
        ///
        /// Порядок: сбросить наши прошлые записи (иначе они станут входом нового замера и панель
        /// сможет только расти) → дать Flexalon померить Body → взять размер Body → посчитать
        /// панель → зафиксировать → подтянуть шапку.
        /// </summary>
        private void MeasureAndFit()
        {
            ResetHeaderWidth();

            _rootFlexalon.WidthType = SizeType.Layout;
            _rootFlexalon.HeightType = SizeType.Layout;
            _rootFlexalon.MinWidthType = MinMaxSizeType.None;
            _rootFlexalon.MinHeightType = MinMaxSizeType.None;

            _rootFlexalon.ForceUpdate();

            var contentWidth = _bodyFlexalon != null && _bodyFlexalon.transform is RectTransform body
                ? body.rect.width + _contentPadding.x + _contentPadding.y
                : 0f;

            var contentHeight = _rootFlexalon.transform is RectTransform measured ? measured.rect.height : 0f;

            var width = Mathf.Max(_minWidth, contentWidth);
            var height = Mathf.Max(_minHeight, contentHeight);

            // Сеттеры Width/Height сами переводят тип в Fixed.
            _rootFlexalon.Width = width;
            _rootFlexalon.Height = height;

            if (_headerFlexalon != null)
            {
                var headerWidth = width - _headerInsetFromPanel * 2f;

                if (headerWidth > 0f)
                {
                    _headerFlexalon.Width = headerWidth;
                }
            }

            _rootFlexalon.ForceUpdate();
        }

        private void ResetHeaderWidth()
        {
            if (_headerFlexalon != null && _headerBaseWidth > 0f)
            {
                _headerFlexalon.Width = _headerBaseWidth;
            }
        }

        internal const string SeparatorKey = "game_txt_or";

        /// <summary>
        /// Локализация, безопасная для редакторского превью: до инициализации сервиса
        /// возвращаем ключ как есть, вместо падения.
        /// </summary>
        internal static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            try
            {
                return LocalizationService.Instance?.GetText(key) ?? key;
            }
            catch
            {
                return key;
            }
        }

        internal static string LocalizeHeader(string nameKey, string suffixKey)
        {
            var name = Localize(nameKey);

            if (string.IsNullOrEmpty(suffixKey))
            {
                return name;
            }

            return $"{name} ({Localize(suffixKey)})";
        }

#if UNITY_EDITOR
        [Title("Превью: вход → выход")]
        [ButtonGroup("вых1")]
        private void В1_Вых1() => Preview(1, 1);

        [ButtonGroup("вых1")]
        private void В2_Вых1() => Preview(2, 1);

        [ButtonGroup("вых1")]
        private void В3_Вых1() => Preview(3, 1);

        [ButtonGroup("вых1")]
        private void В4_Вых1() => Preview(4, 1);

        [ButtonGroup("вых2")]
        private void В1_Вых2() => Preview(1, 2);

        [ButtonGroup("вых2")]
        private void В2_Вых2() => Preview(2, 2);

        [ButtonGroup("вых2")]
        private void В3_Вых2() => Preview(3, 2);

        [ButtonGroup("вых2")]
        private void В4_Вых2() => Preview(4, 2);

        [Title("Превью: только вход")]
        [ButtonGroup("тв")]
        private void Вход1() => Preview(1, 0);

        [ButtonGroup("тв")]
        private void Вход2() => Preview(2, 0);

        [ButtonGroup("тв")]
        private void Вход3() => Preview(3, 0);

        [ButtonGroup("тв")]
        private void Вход4() => Preview(4, 0);

        [Title("Превью: только выход")]
        [ButtonGroup("твых")]
        private void Выход1() => Preview(0, 1, affordable: true);

        [ButtonGroup("твых")]
        private void Выход2() => Preview(0, 2, affordable: true);

        [Title("Превью: раскопка — стоимость + рамка, без стрелки")]
        [InfoBox("Стрелки нет, потому что нет выхода: она показывается только когда есть И вход, И выход. " +
                 "В рамку подставляется картинка из префаба — в игре её задаёт объект уровня " +
                 "(StaticObjectView → Tooltip Settings → Tooltip Preview Icon).")]
        [ButtonGroup("раскоп")]
        private void Раскоп2() => Preview(2, 0);

        [ButtonGroup("раскоп")]
        private void Раскоп3() => Preview(3, 0);

        [ButtonGroup("раскоп")]
        private void Раскоп3_НеХватает() => Preview(3, 0, affordable: false);

        [ButtonGroup("раскоп")]
        private void Раскоп3_Хватает() => Preview(3, 0, affordable: true);

        [Title("Превью: альтернативы (oder) — всегда две")]
        [ButtonGroup("alt")]
        private void Альт_1() => Preview(1, 0, groups: 2);

        [ButtonGroup("alt")]
        private void Альт_2() => Preview(2, 0, groups: 2);

        [ButtonGroup("alt")]
        private void Альт_3() => Preview(3, 0, groups: 2);

        [ButtonGroup("alt")]
        private void Альт_4() => Preview(4, 0, groups: 2);

        [Title("Превью: состояние")]
        [ButtonGroup("st")]
        private void Хватает() => Preview(3, 2, affordable: true);

        [ButtonGroup("st")]
        private void НеХватает() => Preview(3, 2);

        [ButtonGroup("st")]
        private void БезИмени() => Preview(3, 2, showName: false);

        private void Preview(int inputs, int outputs, bool affordable = false, int groups = 1,
            bool showName = true)
        {
            var data = Demo(inputs, outputs, affordable, groups, showName);

            // Картинку берём ту, что уже лежит в префабе: в игре её ставит объект уровня,
            // а в превью показываем заглушку художника — иначе рамка была бы пустой,
            // да ещё и погасла бы (SetPreview прячет блок при null).
            data.PreviewIcon = _previewImage != null ? _previewImage.sprite : null;

            SetCardData(data);

            // Без этого шапка не подтянется под новую ширину прямо в редакторе.
            ForceUpdate();
        }

        private static TooltipCardData Demo(int inputs, int outputs, bool affordable, int groups,
            bool showName = true)
        {
            var data = new TooltipCardData
            {
                NameKey = showName ? "Древние ворота древн" : null,
                IsAffordable = affordable,
            };

            for (var g = 0; g < groups; g++)
            {
                var group = new TooltipGroupData { IsAffordable = affordable };

                for (var i = 0; i < inputs; i++)
                {
                    // Первый ресурс всегда «хватает» — чтобы на превью было видно оба цвета сразу.
                    var enough = affordable || i == 0;

                    group.Cells.Add(new TooltipCellData(null, ((i + 6) * 11).ToString(),
                        enough ? TooltipCellState.Enough : TooltipCellState.NotEnough));
                }

                if (group.Cells.Count > 0)
                {
                    data.InputGroups.Add(group);
                }
            }

            for (var i = 0; i < outputs; i++)
            {
                data.Output.Add(new TooltipCellData(null, ((i + 8) * 11).ToString(), TooltipCellState.Neutral));
            }

            return data;
        }
#endif
    }
}
