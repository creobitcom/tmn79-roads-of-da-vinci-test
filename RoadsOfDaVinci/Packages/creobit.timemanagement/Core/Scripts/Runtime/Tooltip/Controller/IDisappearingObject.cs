using System.Threading;
using UltEvents;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tooltip.Controller
{
    public interface IDisappearingObject
    {
        public UltEvent OnShow { get; set; }
        public UltEvent OnHide { get; set; }
        public float Duration { get; set; }
        public CancellationTokenSource HideCancellation { get; }
        public void Show();
        public void ForceHide();
    }
}