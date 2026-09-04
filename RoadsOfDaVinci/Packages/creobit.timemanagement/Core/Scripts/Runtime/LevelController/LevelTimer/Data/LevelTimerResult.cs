namespace _8floor.TimeManagement.Core.Scripts.Runtime.LevelController.LevelTimer.Data
{
    public class LevelTimerResult
    {
        public float RemainingTime { get; private set; }

        public byte NumberOfStars { get; set; }

        public LevelTimerResult(float remainingTime)
        {
            RemainingTime = remainingTime;
        }
    }
}