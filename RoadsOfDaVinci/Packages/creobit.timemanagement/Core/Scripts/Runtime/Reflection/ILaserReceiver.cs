namespace _8floor.TimeManagement.Core.Scripts.Runtime.Reflection
{
    public interface ILaserReceiver
    {
        bool IsTransparent { get; }
        void OnLaserEnter();
        void OnLaserExit();
    }
}