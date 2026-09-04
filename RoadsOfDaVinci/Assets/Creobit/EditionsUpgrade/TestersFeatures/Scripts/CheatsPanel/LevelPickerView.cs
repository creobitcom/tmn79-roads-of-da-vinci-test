using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    public class LevelPickerView : MonoBehaviour
    {
        private const int COLUMNS_AMOUNT = 8;

        private static readonly Vector2 CellSize = new Vector2(54f, 42f);
        private static readonly Vector2 CellSpacing = new Vector2(4f, 4f);

        private Action<int> _levelPicked;

        private TMP_FontAsset _font;

        public static LevelPickerView Show(Transform parent, int levelsAmount, int lastUnlockedLevel,
            Action<int> levelPicked)
        {
            GameObject root = new GameObject(nameof(LevelPickerView), typeof(RectTransform), typeof(Image),
                typeof(LevelPickerView));

            root.layer = parent.gameObject.layer;

            RectTransform rectTransform = (RectTransform)root.transform;

            rectTransform.SetParent(parent, false);
            rectTransform.SetAsLastSibling();

            Stretch(rectTransform);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;
            root.AddComponent<GraphicRaycaster>();

            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            LevelPickerView view = root.GetComponent<LevelPickerView>();

            view._levelPicked = levelPicked;
            // Reuse whatever font the cheats UI already uses, so this does not depend
            // on a default font asset being set in the TMP settings.
            view._font = parent.GetComponentInChildren<TMP_Text>(true)?.font;

            view.Build(levelsAmount, lastUnlockedLevel);

            return view;
        }

        private void Build(int levelsAmount, int lastUnlockedLevel)
        {
            Button closeButton = gameObject.AddComponent<Button>();

            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(Close);

            CreateTitle($"Level — green 1..{lastUnlockedLevel} unlocked, orange unlocks on tap. Tap outside to close");

            GameObject grid = CreateChild("Grid", transform);

            RectTransform gridRectTransform = (RectTransform)grid.transform;

            gridRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            gridRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gridRectTransform.pivot = new Vector2(0.5f, 0.5f);
            gridRectTransform.anchoredPosition = Vector2.zero;

            GridLayoutGroup layoutGroup = grid.AddComponent<GridLayoutGroup>();

            layoutGroup.cellSize = CellSize;
            layoutGroup.spacing = CellSpacing;
            layoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layoutGroup.constraintCount = COLUMNS_AMOUNT;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;

            ContentSizeFitter sizeFitter = grid.AddComponent<ContentSizeFitter>();

            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int levelNum = 1; levelNum <= levelsAmount; levelNum++)
            {
                CreateLevelButton(grid.transform, levelNum, levelNum <= lastUnlockedLevel);
            }
        }

        private void CreateTitle(string title)
        {
            GameObject titleObject = CreateChild("Title", transform);

            RectTransform rectTransform = (RectTransform)titleObject.transform;

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -20f);
            rectTransform.sizeDelta = new Vector2(0f, 30f);

            CreateText(titleObject, title, 18f);
        }

        private void CreateLevelButton(Transform parent, int levelNum, bool isUnlocked)
        {
            GameObject buttonObject = CreateChild($"Level {levelNum}", parent);

            Image image = buttonObject.AddComponent<Image>();

            image.color = isUnlocked ? new Color(0.16f, 0.42f, 0.2f) : new Color(0.45f, 0.32f, 0.1f);

            Button button = buttonObject.AddComponent<Button>();

            button.targetGraphic = image;
            button.onClick.AddListener(() => OnLevelButtonClicked(levelNum));

            GameObject textObject = CreateChild("Text", buttonObject.transform);

            Stretch((RectTransform)textObject.transform);

            CreateText(textObject, levelNum.ToString(), 20f);
        }

        private void CreateText(GameObject parent, string value, float fontSize)
        {
            TextMeshProUGUI text = parent.AddComponent<TextMeshProUGUI>();

            if (_font != null)
            {
                text.font = _font;
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));

            child.layer = gameObject.layer;
            child.transform.SetParent(parent, false);

            return child;
        }

        private void OnLevelButtonClicked(int levelNum)
        {
            Action<int> levelPicked = _levelPicked;

            Close();

            levelPicked?.Invoke(levelNum);
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
