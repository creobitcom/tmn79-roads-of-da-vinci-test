namespace Creobit.Logger
{
    /// <summary>
    /// The Log class provides static logging utilities for different stages and
    /// components of the application. It includes predefined tags for categorizing
    /// logs and a builder for creating custom logs.
    /// </summary>
    public static class Log
    {
        public static readonly BuilderLogPool Builder = new(new TagLog(string.Empty), 5);
        
        public static readonly TagLog Bootstrap = new("BOOTSTRAP");

        public static readonly TagLog Loading = new("LOADING");

        public static readonly TagLog Meta = new("META");

        public static readonly TagLog Gameplay = new("GAMEPLAY");
    }
}