using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View
{
    public class UnitView : MonoBehaviour
    {
        [SerializeField]
        private Image _workerImage;

        [SerializeField]
        private TMP_Text _freeWorkersCount;

        private int _maxWorkers;
        private int _freeWorkers;
        
        public void SetIcon(Sprite workerIcon)
        {
            _workerImage.sprite = workerIcon;
        }

        public void ReloadUnitView()
        {
            _freeWorkers = 0;
            
            _maxWorkers = 0;
        }

        public void ChangeMaxWorkers(int changeValue)
        {
            _maxWorkers = (int) Mathf.Max(_maxWorkers + changeValue, 0f);
            
            _freeWorkers = Mathf.Clamp(_freeWorkers, 0, _maxWorkers);
            
            SetText();
        }

        public void ChangeFreeWorkers(int changeValue)
        {
            _freeWorkers = Mathf.Clamp(_freeWorkers + changeValue, 0, _maxWorkers);
            
            SetText();
        }

        private void SetText()
        {
            _freeWorkersCount.gameObject.SetActive(_maxWorkers > 1);
            
            _freeWorkersCount.text = $"{_freeWorkers}/{_maxWorkers}";
        }
    }
}