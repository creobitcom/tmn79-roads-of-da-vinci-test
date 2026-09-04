using System;
using System.Collections.Generic;
using System.Linq;
using Creobit.Localization;
using TMPro;
using UnityEngine;

namespace Creobit.UI.Utility
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedFontChanger : MonoBehaviour
    {
        [Serializable]
        public class LocaleFont
        {
            [field: SerializeField] public TMP_FontAsset Font { get; private set; }
            [field: SerializeField] public string Locale { get; private set; }
        }
        private TMP_Text _text;
        
        [SerializeField] private List<LocaleFont> _localeFonts = new List<LocaleFont>();
        [SerializeField] private TMP_FontAsset _defaultFont;
        
        
        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            LocalizationService.Instance.OnLanguageChanged += LocaleChangedHandler;
        }

        private void OnDestroy()
        {
            LocalizationService.Instance.OnLanguageChanged -= LocaleChangedHandler;
        }
        private void LocaleChangedHandler(string language)
        {
            _text.font = _localeFonts.Any(x=>language.Contains(x.Locale)) ? _localeFonts.First(x=>language.Contains(x.Locale)).Font : _defaultFont;
        }
    }
}