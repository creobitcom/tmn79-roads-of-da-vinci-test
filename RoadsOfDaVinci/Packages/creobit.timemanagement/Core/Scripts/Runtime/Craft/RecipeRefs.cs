using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.ObjectView.StaticObject
{
    /// <summary>
    /// Карточка одного рецепта: кнопка (клик = крафт), до 4 слотов входа с раскладкой
    /// по количеству ингредиентов (1 / 2 / 2+1 / 2×2), слот результата и рамки синяя/серая.
    /// Позиции слотов для каждого режима задаются в инспекторе; код только включает нужный режим.
    /// </summary>
    public class RecipeRefs : MonoBehaviour
    {
        public Button button;
        public RecipeData data;

        [Title("Слоты")]
        [Tooltip("Слоты входа (до 4). Порядок важен — код заполняет слева направо.")]
        public RecipeResourceView[] inputSlots = new RecipeResourceView[4];

        [Tooltip("Слот результата (crafted item).")]
        public RecipeResourceView outputSlot;

        [Title("Рамка результата")]
        [Tooltip("Синяя рамка — рецепт можно скрафтить.")]
        public GameObject blueFrame;

        [Tooltip("Серая рамка — ресурсов не хватает.")]
        public GameObject greyFrame;

        [Title("Позиции входных слотов по числу ингредиентов")]
        [Tooltip("1 ингредиент.")]
        public Vector2[] positionsFor1 = { new Vector2(-76f, 10f) };

        [Tooltip("2 ингредиента (в ряд).")]
        public Vector2[] positionsFor2 = { new Vector2(-103.6f, 10f), new Vector2(-48.5f, 10f) };

        [Tooltip("3 ингредиента (2 сверху + 1 по центру снизу).")]
        public Vector2[] positionsFor3 =
            { new Vector2(-103.6f, 52f), new Vector2(-48.5f, 52f), new Vector2(-76f, -31.2f) };

        [Tooltip("4 ингредиента (сетка 2×2).")]
        public Vector2[] positionsFor4 =
        {
            new Vector2(-103.6f, 52f), new Vector2(-48.5f, 52f),
            new Vector2(-102.9f, -31.2f), new Vector2(-48.5f, -31.2f)
        };

        /// <summary>
        /// Включает нужное количество слотов и расставляет их по позициям режима.
        /// </summary>
        public void SetInputLayout(int count)
        {
            var positions = GetPositions(count);

            for (var i = 0; i < inputSlots.Length; i++)
            {
                if (inputSlots[i] == null)
                {
                    continue;
                }

                var active = i < count;
                inputSlots[i].gameObject.SetActive(active);

                if (active && positions != null && i < positions.Length)
                {
                    ((RectTransform)inputSlots[i].transform).anchoredPosition = positions[i];
                }
            }
        }

        /// <summary>
        /// Переключает рамку результата и доступность кнопки: синяя+кликабельно / серая+заблокировано.
        /// </summary>
        public void SetCraftable(bool canCraft)
        {
            if (blueFrame != null)
            {
                blueFrame.SetActive(canCraft);
            }

            if (greyFrame != null)
            {
                greyFrame.SetActive(!canCraft);
            }

            if (button != null)
            {
                button.interactable = canCraft;
            }
        }

        private Vector2[] GetPositions(int count)
        {
            switch (count)
            {
                case 1: return positionsFor1;
                case 2: return positionsFor2;
                case 3: return positionsFor3;
                default: return positionsFor4;
            }
        }

#if UNITY_EDITOR
        [Title("Превью (только редактор)")]
        [ButtonGroup("preview")]
        private void Вход1() => SetInputLayout(1);

        [ButtonGroup("preview")]
        private void Вход2() => SetInputLayout(2);

        [ButtonGroup("preview")]
        private void Вход3() => SetInputLayout(3);

        [ButtonGroup("preview")]
        private void Вход4() => SetInputLayout(4);
#endif
    }
}
