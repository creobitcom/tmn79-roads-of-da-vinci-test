using System;
using UnityEngine.SceneManagement;

namespace Creobit.EditionsUpgrade
{
    public class ScenesCheat : ICheat
    {
        public string Title { get; private set; }

        private string[] _availableOnScenesNames;

        private Action _command;

        public ScenesCheat(string title, string[] availableOnScenesNames, Action command)
        {
            Title = title;

            _availableOnScenesNames = availableOnScenesNames;

            _command = command;
        }

        public bool IsExecutable
        {
            get
            {
                string currentSceneName = SceneManager.GetActiveScene().name;

                for (int i = 0; i < _availableOnScenesNames.Length; i++)
                {
                    if (currentSceneName.ToLower() == _availableOnScenesNames[i].ToLower())
                    {
                        return true;
                    }
                }

                return false;
            }
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
