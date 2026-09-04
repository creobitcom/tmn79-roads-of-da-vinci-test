namespace Creobit.EditionsUpgrade
{
    public class ProjectScenes
    {

#if TOYMAN
        public string Menu => "MainMenu";
        public string LevelsSelector => "MetaMap";
        public string[] Levels => new string[] { "Game_temp" };

#elif GAME_ON
        public string Menu => "Main";
        public string LevelsSelector => "Map";
        public string[] Levels
        {
            get
            {
                string[] result = new string[LEVELS_AMOUNT];

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = $"Level {i + 1}";
                }

                return result;
            }
        }

        private const int LEVELS_AMOUNT = 50;
#elif ARGUNOV
        public string Menu => "Menu";
        public string LevelsSelector => "Map";
        public string[] Levels => new string[] { "Game" };
#elif CREOBIT
        public string Meta => "Meta";
        public string Gameplay => "Gameplay";
#endif
    }
}