using System;
using System.Collections.Generic;
using _8floor.TimeManagement.Core.Scripts.Runtime.Ambience;
using Creobit.Bootstrap.Core.Scripts.Runtime.Cheats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Creobit.EditionsUpgrade
{
    public class AmbiencePickerView : MonoBehaviour
    {
        private const float PANEL_WIDTH = 272f;

        private static readonly Color ActiveColor = new Color(0.16f, 0.42f, 0.2f);
        private static readonly Color DisabledColor = new Color(0.45f, 0.16f, 0.16f);

        private sealed class LayerToggle
        {
            public string Label;
            public Func<bool> IsDisabled;
            public Action Flip;
            public Image Image;
            public TMP_Text Text;
        }

        private readonly List<LayerToggle> _layerToggles = new List<LayerToggle>();

        private TMP_FontAsset _font;
        private TMP_Text _titleText;
        private TMP_Text _toggleText;
        private Image _toggleImage;

        public static AmbiencePickerView Show(Transform parent)
        {
            GameObject root = new GameObject(nameof(AmbiencePickerView), typeof(RectTransform), typeof(Image),
                typeof(AmbiencePickerView));

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

            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            AmbiencePickerView view = root.GetComponent<AmbiencePickerView>();

            TMP_Text sourceText = parent.GetComponentInChildren<TMP_Text>(true);

            view._font = sourceText == null ? null : sourceText.font;

            view.Build();

            return view;
        }

        private void Build()
        {
            Button closeButton = gameObject.AddComponent<Button>();

            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(Close);

            GameObject panel = CreateChild("Panel", transform);

            RectTransform panelRectTransform = (RectTransform)panel.transform;

            panelRectTransform.anchorMin = new Vector2(0f, 0.5f);
            panelRectTransform.anchorMax = new Vector2(0f, 0.5f);
            panelRectTransform.pivot = new Vector2(0f, 0.5f);
            panelRectTransform.anchoredPosition = new Vector2(24f, 0f);
            panelRectTransform.sizeDelta = new Vector2(PANEL_WIDTH, 0f);

            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            VerticalLayoutGroup layoutGroup = panel.AddComponent<VerticalLayoutGroup>();

            layoutGroup.padding = new RectOffset(8, 8, 8, 8);
            layoutGroup.spacing = 6f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;

            ContentSizeFitter sizeFitter = panel.AddComponent<ContentSizeFitter>();

            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateTitle(panel.transform);
            CreateToggle(panel.transform);
            CreateLayerToggles(panel.transform);
            CreateHint(panel.transform);

            Refresh();

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRectTransform);
        }

        private void CreateTitle(Transform parent)
        {
            GameObject titleObject = CreateChild("Title", parent);

            titleObject.AddComponent<LayoutElement>().minHeight = 26f;

            _titleText = CreateText(titleObject, "Ambience", 17f);
        }

        private void CreateToggle(Transform parent)
        {
            GameObject toggleObject = CreateChild("Toggle", parent);

            toggleObject.AddComponent<LayoutElement>().minHeight = 40f;

            _toggleImage = toggleObject.AddComponent<Image>();
            _toggleImage.color = DisabledColor;

            Button button = toggleObject.AddComponent<Button>();

            button.targetGraphic = _toggleImage;
            button.onClick.AddListener(OnToggleClicked);

            GameObject textObject = CreateChild("Text", toggleObject.transform);

            Stretch((RectTransform)textObject.transform);

            _toggleText = CreateText(textObject, "OFF", 18f);
        }

        private void CreateLayerToggles(Transform parent)
        {
            AddLayerToggle(parent, "Sprite light",
                () => LevelAmbience.DebugDisableSpriteLighting,
                () => LevelAmbience.DebugDisableSpriteLighting = !LevelAmbience.DebugDisableSpriteLighting);
            AddLayerToggle(parent, "Point lights",
                () => LevelAmbience.DebugDisablePointLights,
                () => LevelAmbience.DebugDisablePointLights = !LevelAmbience.DebugDisablePointLights);
            AddLayerToggle(parent, "Haze / fog",
                () => LevelAmbience.DebugDisableHaze,
                () => LevelAmbience.DebugDisableHaze = !LevelAmbience.DebugDisableHaze);
            AddLayerToggle(parent, "Particles",
                () => LevelAmbience.DebugDisableParticles,
                () => LevelAmbience.DebugDisableParticles = !LevelAmbience.DebugDisableParticles);
            AddLayerToggle(parent, "Tilt-shift",
                () => LevelAmbience.DebugDisableTiltShift,
                () => LevelAmbience.DebugDisableTiltShift = !LevelAmbience.DebugDisableTiltShift);
            AddLayerToggle(parent, "Bloom / vignette",
                () => LevelAmbience.DebugDisablePostFx,
                () => LevelAmbience.DebugDisablePostFx = !LevelAmbience.DebugDisablePostFx);
        }

        private void AddLayerToggle(Transform parent, string label, Func<bool> isDisabled, Action flip)
        {
            GameObject buttonObject = CreateChild(label, parent);

            buttonObject.AddComponent<LayoutElement>().minHeight = 34f;

            Image image = buttonObject.AddComponent<Image>();

            Button button = buttonObject.AddComponent<Button>();

            button.targetGraphic = image;

            GameObject textObject = CreateChild("Text", buttonObject.transform);

            Stretch((RectTransform)textObject.transform);

            LayerToggle toggle = new LayerToggle
            {
                Label = label,
                IsDisabled = isDisabled,
                Flip = flip,
                Image = image,
                Text = CreateText(textObject, label, 15f)
            };

            button.onClick.AddListener(() =>
            {
                toggle.Flip();
                Refresh();
            });

            _layerToggles.Add(toggle);
        }

        private void CreateHint(Transform parent)
        {
            GameObject hintObject = CreateChild("Hint", parent);

            hintObject.AddComponent<LayoutElement>().minHeight = 22f;

            CreateText(hintObject, "tap outside to close", 13f);
        }

        private void OnToggleClicked()
        {
            GameplayCheats.SetAmbienceEnabled(!GameplayCheats.IsAmbienceEnabled());

            Refresh();
        }

        private void Refresh()
        {
            LevelAmbience ambience = GameplayCheats.FindAmbience();

            bool isEnabled = ambience != null && ambience.enableAmbience;

            _toggleText.text = isEnabled ? "ON" : "OFF";
            _toggleImage.color = isEnabled ? ActiveColor : DisabledColor;

            _titleText.text = ambience == null
                ? "Ambience — not found on level"
                : "Ambience layers";

            foreach (LayerToggle toggle in _layerToggles)
            {
                bool layerOn = !toggle.IsDisabled();

                toggle.Image.color = layerOn ? ActiveColor : DisabledColor;
                toggle.Text.text = toggle.Label + (layerOn ? ": ON" : ": OFF");
            }
        }

        private TMP_Text CreateText(GameObject parent, string value, float fontSize)
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

            return text;
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));

            child.layer = gameObject.layer;
            child.transform.SetParent(parent, false);

            return child;
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
