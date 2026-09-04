namespace _8floor.TimeManagement.Core.Scripts.Runtime.Tutorial.Animations
{
    /// <summary>
    /// Тип анимации появления/исчезновения. Несколько типов можно комбинировать в одном наборе —
    /// они играют одновременно, у каждого своя длительность и задержка.
    /// </summary>
    public enum TutorialAnimationKind
    {
        /// <summary>Прозрачность.</summary>
        Fade = 0,

        /// <summary>Масштаб.</summary>
        Scale = 1,

        /// <summary>Сдвиг с указанной стороны.</summary>
        Move = 2,

        /// <summary>Поворот.</summary>
        Rotate = 3,

        /// <summary>Пружинка на месте.</summary>
        Punch = 4,
    }

    /// <summary>
    /// Сторона, с которой прилетает окно при <see cref="TutorialAnimationKind.Move"/>.
    /// </summary>
    public enum TutorialAnimationDirection
    {
        Left = 0,
        Right = 1,
        Up = 2,
        Down = 3,
    }
}
