using _8floor.TimeManagement.Core.Scripts.Runtime.LevelController;
using Creobit.Logger;
using Flexalon;
using TMPro;
using UltEvents;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip
{
    public class TooltipResourceView : MonoBehaviour
    {
        [SerializeField]
        private Image _tooltipResourceImage;
        
        [SerializeField]
        private TMP_Text _tooltipResourceText;

        [SerializeField]
        public UltEvent OnResourceNotEnough;
        
        [SerializeField]
        public UltEvent OnResourceEnough;

        [SerializeField]
        public UltEvent OnResourceIsOutput;

        public void SetData(ResourceAmount tooltipResource, bool resourceEnough = true, bool isOutput = false)
        {
            _tooltipResourceImage.gameObject.SetActive(true);

            _tooltipResourceImage.sprite = tooltipResource.Resource.Image;
            _tooltipResourceText.text = tooltipResource.Amount.ToString();

            if(_tooltipResourceText.TryGetComponent(out FlexalonObject flexalon))
            {
                flexalon.ForceUpdate();
            }

            if(isOutput)
            {
                OnResourceIsOutput.InvokeSafe();
                return;
            }

            if (resourceEnough)
            {
                OnResourceEnough.InvokeSafe();
            }
            else
            {
                OnResourceNotEnough.InvokeSafe();
                Log.Gameplay.Info("Not enough resources!");
            }
        }

        public void SetAsSeparator(string separatorText)
        {
            _tooltipResourceImage.gameObject.SetActive(false);
            
            _tooltipResourceText.text = separatorText;

            OnResourceEnough.InvokeSafe();

            if(_tooltipResourceText.TryGetComponent(out FlexalonObject flexalon))
            {
                flexalon.ForceUpdate();
            }
        }
    }
}