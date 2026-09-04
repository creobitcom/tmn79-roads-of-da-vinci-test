namespace _8floor.TimeManagement.Core.Scripts.Runtime.VideoCutscenes.Data
{
    /// <summary>
    /// Когда катсцена показывается. Условия зеркалят ComicsConditions,
    /// чтобы видео можно было ставить в те же точки, что и комикс.
    /// </summary>
    public enum VideoCutsceneTrigger
    {
        /// <summary>Только вручную (из UltEvent через VideoCutscenesBridge).</summary>
        Manual = 0,

        /// <summary>Первый вход в игру на профиле (аналог ComicsConditions.AnyCondition).</summary>
        OnNewProfile = 1,

        /// <summary>Перед запуском уровня с номером Level.</summary>
        BeforeLevel = 2,

        /// <summary>После прохождения уровня с номером Level.</summary>
        AfterLevel = 3
    }
}
