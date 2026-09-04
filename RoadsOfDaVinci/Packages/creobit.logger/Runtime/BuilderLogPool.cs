using System;
using System.Collections.Generic;
using System.Text;

namespace Creobit.Logger
{
    public sealed class BuilderLogPool
    {
        private readonly Stack<LogPacker> _buildersPool = new();
        private readonly TagLog _log;
        private readonly int _buildersCount;
        private readonly object _sync = new object();

        /// <summary>
        /// Represents a pool of <see cref="LogPacker"/> instances used for efficiently
        /// building and logging messages within the application.
        /// Ensures reuse of log packer instances to minimize object allocation overhead.
        /// </summary>
        public BuilderLogPool(TagLog log, int buildersCount)
        {
            if (buildersCount <= 0) throw new ArgumentOutOfRangeException(nameof(buildersCount), "Pool size must be greater than zero.");

            _log = log ?? throw new ArgumentNullException(nameof(log));
            _buildersCount = buildersCount;

            for (var i = 0; i < buildersCount; i++)
                _buildersPool.Push(new LogPacker());
        }

        /// <summary>
        /// Attempts to retrieve an available <see cref="LogPacker"/> instance from the pool.
        /// If no instances are available, an exception is thrown.
        /// Ensures the returned <see cref="LogPacker"/> is cleared and ready for use.
        /// </summary>
        /// <returns>A <see cref="LogPacker"/> instance from the pool for message building.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no <see cref="LogPacker"/> instances are available in the pool.</exception>
        public LogPacker Rent()
        {
            lock (_sync)
            {
                if (_buildersPool.Count > 0) {
                    var sb = _buildersPool.Pop();
                    sb.Clear();
                    return sb;
                }
            }

            throw new InvalidOperationException("No LogPacker instances available to rent from the pool.");
        }

        /// <summary>
        /// Returns a <see cref="LogPacker"/> instance back to the pool after logging its content.
        /// Ensures that the log packer is cleared and re-added to the pool for reuse.
        /// </summary>
        /// <param name="sb">The <see cref="LogPacker"/> instance to be returned and logged. Must not be null.</param>
        /// <param name="additionalTag">
        /// An optional tag to prefix the log message.
        /// If null or empty, the log will only include the log packer's content.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="sb"/> argument is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if returning the log packer exceeds the pool's capacity
        /// or if the instance is not from the pool.
        /// This typically indicates an incorrect usage of the method.
        /// </exception>
        public void ReturnAndLog(LogPacker sb, string additionalTag = null)
        {
            if (sb is null) throw new ArgumentNullException(nameof(sb));

            if (string.IsNullOrEmpty(additionalTag))
                _log.Info(sb.ToString());
            else
                _log.Info(additionalTag, sb.ToString());

            lock (_sync)
            {
                if (_buildersPool.Count >= _buildersCount) {
                    throw new InvalidOperationException("Pool capacity exceeded. Possible double-return or foreign LogPacker.");
                }

                sb.Clear();
                _buildersPool.Push(sb);
            }
        }
    }

    /// <summary>
    /// Provides functionality to build and manipulate log messages using a reusable string builder.
    /// Designed for efficient formatting, appending, and clearing of log content during runtime.
    /// </summary>
    public sealed class LogPacker
    {
        private readonly StringBuilder _sb = new();

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends the text representation of the specified object, using the specified format string,
        /// to the end of the current instance.
        /// </summary>
        /// <param name="format">
        /// A composite format string that includes format items, which correspond to objects in the argument list.
        /// </param>
        /// <param name="arg0">
        /// The first object to format and append.
        /// </param>
        public void AppendFormat(string format, object arg0)
        {
            _sb.AppendFormat(format, arg0);
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends the text representation of the specified object, using the specified format string,
        /// to the end of the current instance.
        /// </summary>
        /// <param name="format">
        /// A composite format string that includes format items, which correspond to objects in the argument list.
        /// </param>
        /// <param name="arg0">
        /// The first object to format and append.
        /// </param>
        /// <param name="arg1">
        /// The second object to format and append.
        /// </param>
        public void AppendFormat(string format, object arg0, object arg1)
        {
            _sb.AppendFormat(format, arg0, arg1);
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends the text representation of the specified objects, using the specified format string,
        /// to the end of the current instance.
        /// </summary>
        /// <param name="format">
        /// A composite format string that includes format items, which correspond to objects in the argument list.
        /// </param>
        /// <param name="arg0">
        /// The first object to format and append.
        /// </param>
        /// <param name="arg1">
        /// The second object to format and append.
        /// </param>
        /// <param name="arg2">
        /// The third object to format and append.
        /// </param>
        public void AppendFormat(string format,
            object arg0,
            object arg1,
            object arg2)
        {
            _sb.AppendFormat(format, arg0, arg1, arg2);
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends the text representation of the specified object or objects, using the specified format string,
        /// to the end of the current instance.
        /// </summary>
        /// <param name="format">
        /// A composite format string that includes format items, which correspond to objects in the argument list.
        /// </param>
        /// <param name="args">
        /// An array that contains zero or more objects to format and append.
        /// </param>
        public void AppendFormat(string format, params object[] args)
        {
            _sb.AppendFormat(format, args);
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends a line terminator to the end of the current instance.
        /// This method inserts the default line terminator (typically a newline character)
        /// without appending any additional content.
        /// </summary>
        public void AppendLine()
        {
            _sb.AppendLine();
        }

#if CREOBIT_PROD
        [System.Diagnostics.Conditional("DUMMY_UNUSED_DEFINE")]
#endif
        /// <summary>
        /// Appends the default line terminator to the end of the current instance.
        /// </summary>
        public void AppendLine(string value)
        {
            _sb.AppendLine(value);
        }

        /// <summary>
        /// Clears all content from the current instance of <see cref="LogPacker"/>,
        /// resetting it to an empty state. Helps in reusing the instance
        /// without unnecessary creation of new objects.
        /// </summary>
        public void Clear()
        {
            _sb.Clear();
        }

        /// <summary>
        /// Ensures that the capacity in the current string builder
        /// is at least the specified value. If the current capacity is less than the specified capacity,
        /// it is increased to the specified value.
        /// </summary>
        /// <param name="capacity">
        /// The minimum capacity that the string builder must support.
        /// </param>
        /// <returns>
        /// The new capacity of the string builder, which will be at least equal to
        /// the specified value.
        /// </returns>
        public int EnsureCapacity(int capacity) => _sb.EnsureCapacity(capacity);

        /// <summary>
        /// Determines whether the current instance is equal to another <see cref="LogPacker"/> instance.
        /// Compares the underlying string builder for equality.
        /// </summary>
        /// <param name="sb">
        /// Another instance of <see cref="LogPacker"/> to compare with the current instance.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if the underlying string builder of the given instance is equal to the current instance;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(LogPacker sb) => _sb.Equals(sb._sb);

        /// <summary>
        /// Converts the current instance of <see cref="LogPacker"/> to its string representation,
        /// providing the formatted log message as a single string.
        /// </summary>
        /// <returns>
        /// A string representation of the log content contained within the current instance.
        /// </returns>
        public override string ToString() => _sb.ToString();

        /// <summary>
        /// Gets or sets the maximum number of characters that the internal buffer of the underlying
        /// <see cref="StringBuilder"/> instance can currently hold, without resizing.
        /// </summary>
        /// <remarks>
        /// When setting this property, if the specified value is less than the current length of the content,
        /// an <see cref="ArgumentOutOfRangeException"/> is thrown.
        /// Increasing the capacity reallocates the internal
        /// buffer to accommodate the specified capacity without truncating existing content.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when attempting to set the capacity to a value less than the current length of the content,
        /// or greater than the <see cref="MaxCapacity"/>.
        /// </exception>
        public int Capacity
        {
            get => _sb.Capacity;
            set => _sb.Capacity = value;
        }

        /// <summary>
        /// Represents the current instance of the class from which it is accessed.
        /// It is used to reference members or methods of the same instance.
        /// </summary>
        public char this[int index]
        {
            get => _sb[index];
            set => _sb[index] = value;
        }

        /// <summary>
        /// Gets or sets the length of the string stored in the internal StringBuilder.
        /// </summary>
        /// <value>
        /// The number of characters in the string contained in the internal StringBuilder.
        /// </value>
        /// <remarks>
        /// When setting this property, if the specified length is less than the current length of
        /// the string, the string is truncated.
        /// If the specified length is greater than the
        /// current length of the string, null characters are appended to the end of the string
        /// to reach the desired length.
        /// </remarks>
        public int Length
        {
            get => _sb.Length;
            set => _sb.Length = value;
        }

        /// <summary>
        /// Gets the maximum capacity this instance of StringBuilder can allocate to accommodate characters.
        /// </summary>
        /// <remarks>
        /// This property represents the upper limit of the capacity the underlying StringBuilder can grow to.
        /// It is determined when the StringBuilder instance is created and remains constant throughout its lifecycle.
        /// Attempting to exceed this value will result in an exception.
        /// </remarks>
        public int MaxCapacity => _sb.MaxCapacity;
    }
}