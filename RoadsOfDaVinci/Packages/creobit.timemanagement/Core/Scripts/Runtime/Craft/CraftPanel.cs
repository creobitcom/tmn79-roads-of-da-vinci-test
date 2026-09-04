using _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters;
using Creobit.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    /// <summary>
    /// Окно крафта (книжка). Содержит фон-книжку (3 варианта по числу рецептов),
    /// до 3 пред-размещённых карточек рецептов и кнопку закрытия.
    /// Код (CraftController) вызывает SetRecipeCount и наполняет карточки данными.
    /// </summary>
    public class CraftPanel : PanelData
    {
        public GameplayIntervalGeneralParameters craftIntervalParameters;

        [Title("Книжка-фон")]
        [Tooltip("Image фона-книжки. Спрайт меняется по числу рецептов.")]
        public Image bookImage;

        [Tooltip("Спрайты книжки: [0]=1 рецепт, [1]=2, [2]=3 (wooden panel 1/2/3).")]
        public Sprite[] bookSprites = new Sprite[3];

        [Title("Карточки (пред-размещены, максимум 3)")]
        [Tooltip("Инстансы карточек рецептов. Дети книжки. Код показывает нужное количество.")]
        public RecipeRefs[] cards = new RecipeRefs[3];

        [Tooltip("Позиция карточки при 1 рецепте.")]
        public Vector2[] cardPositionsFor1 = { new Vector2(-36f, -2.9f) };

        [Tooltip("Позиции карточек при 2 рецептах.")]
        public Vector2[] cardPositionsFor2 = { new Vector2(-203.6f, -2.9f), new Vector2(129.2f, -2.9f) };

        [Tooltip("Позиции карточек при 3 рецептах.")]
        public Vector2[] cardPositionsFor3 =
            { new Vector2(-370.6f, -2.9f), new Vector2(-38.6f, -2.9f), new Vector2(293f, -2.9f) };

        [Title("Крестик")]
        [Tooltip("Кнопка закрытия. Обработчик вешает CraftController при открытии окна.")]
        public Button closeButton;

        [Tooltip("Позиции крестика по числу рецептов: [0]=1, [1]=2, [2]=3.")]
        public Vector2[] closePositions =
            { new Vector2(174f, 68.5f), new Vector2(341f, 68.5f), new Vector2(506f, 68.5f) };

        /// <summary>
        /// Показывает нужное число карточек, ставит книжку и двигает крестик.
        /// </summary>
        public void SetRecipeCount(int count)
        {
            count = Mathf.Clamp(count, 1, cards.Length);

            if (bookImage != null && bookSprites != null
                && count - 1 < bookSprites.Length && bookSprites[count - 1] != null)
            {
                bookImage.sprite = bookSprites[count - 1];
                bookImage.SetNativeSize();
            }

            var positions = GetCardPositions(count);

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                var active = i < count;
                cards[i].gameObject.SetActive(active);

                if (active && positions != null && i < positions.Length)
                {
                    ((RectTransform)cards[i].transform).anchoredPosition = positions[i];
                }
            }

            if (closeButton != null && closePositions != null && count - 1 < closePositions.Length)
            {
                ((RectTransform)closeButton.transform).anchoredPosition = closePositions[count - 1];
            }
        }

        private Vector2[] GetCardPositions(int count)
        {
            switch (count)
            {
                case 1: return cardPositionsFor1;
                case 2: return cardPositionsFor2;
                default: return cardPositionsFor3;
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [ButtonGroup("preview")]
        private void Показать1() => SetRecipeCount(1);

        [ButtonGroup("preview")]
        private void Показать2() => SetRecipeCount(2);

        [ButtonGroup("preview")]
        private void Показать3() => SetRecipeCount(3);
#endif
    }
}
