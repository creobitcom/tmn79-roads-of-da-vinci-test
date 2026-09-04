using TMPro;
using UnityEngine;

namespace Creobit.EditionsUpgrade
{
    [RequireComponent(typeof(TMP_Text))]
    public class PriceController : MonoBehaviour
    {
#if HAS_AD

#if  HUAWEI
        private HuaweiIAPButton _iapCore;
#else
        private IapCore _iapCore;
#endif
        private TMP_Text _text;
        
        private void Awake()
        {
#if  HUAWEI
            _iapCore = GetComponent<HuaweiIAPButton>();
#else
            _iapCore = GetComponent<IapCore>();
#endif
            _text = GetComponent<TMP_Text>();
            _iapCore.OnUpdatePrice.AddListener(UpdateText);
            
            // Invoke(nameof(UpdateText), 3f);
        }

        private void UpdateText()
        {
            _text.text = _iapCore.IapPrice;
        }
#endif
    }
}