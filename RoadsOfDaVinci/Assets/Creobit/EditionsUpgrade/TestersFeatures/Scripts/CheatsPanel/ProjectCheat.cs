using System;

namespace Creobit.EditionsUpgrade
{
    public class ProjectCheat : ICheat
    {
        public string Title { get; }

        public bool IsExecutable => true;

        private Action _command;

        public ProjectCheat(string title, Action command)
        {
            Title = title;

            _command = command;
        }

        public void Execute()
        {
            if (!IsExecutable)
            {
                return;
            }

            _command();
        }
    }
}
