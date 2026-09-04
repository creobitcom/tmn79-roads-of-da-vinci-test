using System;

namespace _8floor.TimeManagement.Core.Scripts.Runtime.GameplayIntervals.Parameters
{
    public class GameplayIntervalSpecificParameters
    {
        public Action Started { get; private set; }
        public Action LoopStarted { get; private set; }
        public Action LoopPaused { get; private set; }
        public Action LoopUnpaused { get; private set; }
        public Action Ticked { get; private set; }
        public Action Canceled { get; private set; }
        public Action LoopCompleted { get; private set; }
        public Action Completed { get; private set; }
        public MovableObjectTaskView MovableObjectTaskView { get; private set; }
        public ObjectView.ObjectView ObjectView;

        public GameplayIntervalSpecificParameters(
            Action started, 
            Action loopStarted, 
            Action loopPaused, 
            Action loopUnpaused, 
            Action ticked, 
            Action canceled, 
            Action loopCompleted, 
            Action completed, 
            MovableObjectTaskView movableObjectTaskView,
            ObjectView.ObjectView objectView
            )
        {
            Started = started;
            LoopStarted = loopStarted;
            Ticked = ticked;
            Canceled = canceled;
            LoopCompleted = loopCompleted;
            Completed = completed;
            MovableObjectTaskView = movableObjectTaskView;
            LoopPaused = loopPaused;
            LoopUnpaused = loopUnpaused;
            ObjectView = objectView;
        }
    }
}
