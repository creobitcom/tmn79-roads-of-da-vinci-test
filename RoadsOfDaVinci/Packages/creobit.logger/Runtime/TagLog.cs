using System;
using UnityEngine;

namespace Creobit.Logger
{
    /// <summary>
    /// Provides functionality for logging messages categorized by tags.
    /// </summary>
    /// <remarks>
    /// The <c>TagLog</c> class allows for the logging of messages with various levels of severity
    /// (Info, Warning, Error),
    /// with an optional additional tag for further categorization.
    /// It uses Unity's logging system to output messages
    /// to the Unity Console.
    /// </remarks>
    public sealed class TagLog
    {
        private readonly string _tag;
        private readonly string _partTag;

        /// <summary>
        /// Represents a logging system with categorized tags for logging messages of various severity levels.
        /// </summary>
        /// <remarks>
        /// The <c>TagLog</c> class provides methods for logging informational messages, warnings, and errors.
        /// Messages can
        /// be associated with specific tags for categorization,
        /// allowing for a more streamlined and organized debugging process.
        /// This class integrates with Unity's built-in logging framework for output.
        /// </remarks>
        public TagLog(string tag)
        {
            if (string.IsNullOrEmpty(tag)) {
                _tag = string.Empty;
                _partTag = "[";
            }
            else {
                _tag = '[' + tag + "] ";
                _partTag = '[' + tag + ':';
            }
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Logs an informational message with the associated tag.
        /// </summary>
        /// <param name="msg">The message to log. This should describe the relevant information to communicate.</param>
        public void Info(string msg)
        {
            Debug.unityLogger.Log(LogType.Log, _tag, msg);
        }
        
#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Logs an informational message categorized by tags.
        /// </summary>
        /// <param name="additionalTag">An additional tag to further categorize the warning message.</param>
        /// <param name="msg">The message to be logged.</param>
        /// <remarks>
        /// The <c>Info</c> method logs a message with an informational severity level.
        /// It uses the pre-defined tag
        /// associated with the <c>TagLog</c> instance for categorization.
        /// This method is ideal for logging general
        /// information and updates.
        /// </remarks>
        public void Info(string additionalTag, string msg)
        {
            Debug.unityLogger.Log(LogType.Log, GetFullTag(additionalTag), msg);
        }

        /// <summary>
        /// Logs a warning message with the specified content.
        /// </summary>
        /// <param name="msg">The content of the warning message to log.</param>
        /// <remarks>
        /// The <c>Warning</c> method is used to log warning messages that indicate potential issues
        /// or non-critical problems that should be addressed to improve application flow or performance.
        /// This method uses Unity's logging framework to output warning messages.
        /// </remarks>
        public void Warning(string msg)
        {
            Debug.unityLogger.Log(LogType.Warning, _tag, msg);
        }

        /// <summary>
        /// Logs a warning message categorized by tags.
        /// </summary>
        /// <param name="additionalTag">An additional tag to further categorize the warning message.</param>
        /// <param name="msg">The warning message to be logged.</param>
        /// <remarks>
        /// This method logs a warning message, optionally categorized with an additional tag,
        /// using Unity's logging system.
        /// It is useful for identifying potential issues or areas of concern
        /// within the application.
        /// </remarks>
        public void Warning(string additionalTag, string msg)
        {
            Debug.unityLogger.Log(LogType.Warning, GetFullTag(additionalTag), msg);
        }

        /// <summary>
        /// Logs an error message with the associated tag or categorization.
        /// </summary>
        /// <param name="msg">The error message to be logged.</param>
        /// <remarks>
        /// The <c>Error</c> method outputs messages to Unity's logging system with a severity level of error.
        /// It is intended to provide detailed feedback during application runtime, helping to identify issues
        /// that require immediate attention.
        /// </remarks>
        public void Error(string msg)
        {
            Debug.unityLogger.Log(LogType.Error, _tag, msg);
        }

        /// <summary>
        /// Logs an error message with an optional additional tag for categorization.
        /// </summary>
        /// <param name="additionalTag">A string representing an additional tag for categorizing the error log.</param>
        /// <param name="msg">The error message to log.</param>
        /// <remarks>
        /// This method outputs an error message to the Unity Console,
        /// categorized by the specified additional tag, if provided.
        /// It integrates with Unity's logging system for consistent log handling.
        /// </remarks>
        public void Error(string additionalTag, string msg)
        {
            Debug.unityLogger.Log(LogType.Error, GetFullTag(additionalTag), msg);
        }

        /// <summary>
        /// Logs an exception with the associated tag as an error.
        /// </summary>
        /// <param name="e">The exception instance to be logged.</param>
        /// <remarks>
        /// This method outputs the exception's message and call stack to the Unity Console,
        /// allowing for easier debugging of runtime issues.
        /// The method integrates with Unity's logging system and ensures the error log is associated with the predefined tag.
        /// </remarks>
        public void Error(Exception e)
        {
            Debug.unityLogger.Log(LogType.Exception, _tag, e.ToString());
        }

        /// <summary>
        /// Logs an error message with the default tag associated with the logger.
        /// </summary>
        /// <param name="e">The error message to log.</param>
        /// <param name="additionalTag">An additional tag to further categorize the warning message.</param>
        public void Error(string additionalTag, Exception e)
        {
            Debug.unityLogger.Log(LogType.Exception, GetFullTag(additionalTag), e.ToString());
        }

        /// <summary>
        /// Throws a new exception with a message prepended by the instance's logging tag.
        /// </summary>
        /// <param name="msg">The message detailing the cause of the exception.</param>
        /// <exception cref="System.Exception">Always thrown with the provided message and the logging tag.</exception>
        public void ThrowException(string msg)
        {
            throw new Exception(_tag + msg);
        }

        /// <summary>
        /// Throws an exception with a message prefixed by a formatted tag.
        /// </summary>
        /// <param name="additionalTag">The additional tag to include in the exception's message.</param>
        /// <param name="msg">The message to include in the exception.</param>
        public void ThrowException(string additionalTag, string msg)
        {
            throw new Exception(GetFullTag(additionalTag) + msg);
        }

        private string GetFullTag(string additionalTag) => _partTag + additionalTag + "] ";
    }
}