namespace _8floor.TimeManagement.Core.Scripts.Runtime.Utils
{
    public enum ArithmeticModes
    {
        Addition,
        Multiplication,
        Subtraction,
        Division
    }

    public static class Arithmetic
    {
        public static float CountMode(float initialValue, float value, ArithmeticModes mode, bool invertOperation)
        {
            float result = 0f;

            if(mode == ArithmeticModes.Addition)
            {
                result = invertOperation ? initialValue - value : initialValue + value;
            }
            else if(mode == ArithmeticModes.Subtraction)
            {
                result = invertOperation ? initialValue - value : initialValue - value;
            }
            else if(mode == ArithmeticModes.Multiplication)
            {
                result = invertOperation ? initialValue / value : initialValue * value;
            }
            else if(mode == ArithmeticModes.Division)
            {
                result = invertOperation ? initialValue * value : initialValue / value;
            }

            return result;
        }
    }
}
