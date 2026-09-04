using DG.Tweening;
using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.UI
{
    public class Star : MonoBehaviour
    {

        [SerializeField] private DOTweenAnimation _showAnimation;
        [SerializeField] private GameObject _starImage;

        public void ShowStar()
        {

            if(_showAnimation != null)
            {
                _showAnimation.RecreateTweenAndPlay();
            }
            else
            {
                _starImage.SetActive(true);
            }

        }

        public void HideStar()
        {

            _starImage.SetActive(false);

        }

    }
}
