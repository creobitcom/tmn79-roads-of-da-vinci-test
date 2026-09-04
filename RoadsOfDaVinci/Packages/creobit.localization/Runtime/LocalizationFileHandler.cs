using System;
using Creobit.Localization.Utils;
using Cysharp.Threading.Tasks;

namespace Creobit.Localization
{
    /// <summary>
    /// Handles operations related to localization files, such as loading file content and checking file existence.
    /// </summary>
    public class LocalizationFileHandler
    {
        /// <summary>
        /// Asynchronously loads a text file from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the text file that should be loaded.</param>
        /// <returns>
        /// Returns the content of the file as a string if it is successfully loaded; otherwise, throws a <see cref="LocalizationFileLoadException"/> if the content is null or empty.
        /// </returns>
        public async UniTask<string> LoadTextFileAsync(string filePath)
        {
            var content = await StreamingAssetsHelper.LoadTextFileAsync(filePath);
            return string.IsNullOrEmpty(content) ? throw new LocalizationFileLoadException(filePath) : content;
        }

        /// <summary>
        /// Asynchronously checks if a file exists at the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the file that should be checked for existence.</param>
        /// <returns>
        /// Returns true if the file exists and is not empty, otherwise false.
        /// </returns>
        public async UniTask<bool> FileExistsAsync(string filePath)
        {
            var content = await StreamingAssetsHelper.LoadTextFileAsync(filePath);
            return !string.IsNullOrEmpty(content);
        }
    }

    /// <summary>
    /// Represents exceptions that occur during the localization process.
    /// Serves as a base class for more specific localization-related exceptions.
    /// </summary>
    public class LocalizationException : Exception
    {
        /// <summary>
        /// Represents the base exception for errors related to localization operations.
        /// </summary>
        protected LocalizationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Represents a base exception for all localization-specific errors.
        /// </summary>
        protected LocalizationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Represents an exception that is thrown when a requested language is not available within the localization system.
    /// Typically occurs when attempting to load or set a language that is not part of the defined available languages.
    /// </summary>
    public class LanguageNotAvailableException : LocalizationException
    {
        public LanguageNotAvailableException(string language)
            : base($"Language {language} is not available!")
        {
        }
    }

    /// <summary>
    /// Represents an exception that is thrown when a localization file fails to load.
    /// </summary>
    public class LocalizationFileLoadException : LocalizationException
    {
        public LocalizationFileLoadException(string filePath)
            : base($"Failed to load localization file: {filePath}")
        {
        }
    }

    /// <summary>
    /// Represents an exception that is thrown when parsing a localization file fails.
    /// Typically occurs due to issues such as invalid file format or corrupted content.
    /// </summary>
    public class LocalizationFileParseException : LocalizationException
    {
        public LocalizationFileParseException(string language, Exception innerException)
            : base($"Failed to parse localization file for language: {language}", innerException)
        {
        }
    }

    /// <summary>
    /// Represents an exception that is thrown when no language files are found for a specified game.
    /// </summary>
    public class NoLanguagesFoundException : LocalizationException
    {
        public NoLanguagesFoundException(string game)
            : base($"No language files found in {game} folder!")
        {
        }
    }
}