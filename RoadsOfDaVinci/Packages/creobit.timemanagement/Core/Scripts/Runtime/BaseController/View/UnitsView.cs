using UnityEngine;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.BaseController.View
{
    public class UnitsView : MonoBehaviour
    {
        public void SetUnitView(UnitView unitView)
        {
            unitView.transform.SetParent(transform);
        }
    }
}