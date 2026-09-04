// UltEvents // https://kybernetik.com.au/ultevents // Copyright 2021-2025 Kybernetik //

using System;
using System.Text;
using UnityEngine;

namespace UltEvents
{
    /// <summary>[Assert-Conditional]
    /// A <see cref="HelpURLAttribute"/> which points to the UltEvents documentation.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
    public class UltEventsHelpUrlAttribute : HelpURLAttribute
    {
        /************************************************************************************************************************/

        /// <summary>Creates a new <see cref="UltEventsHelpUrlAttribute"/>.</summary>
        public UltEventsHelpUrlAttribute(string url)
            : base(url)
        { }

        /************************************************************************************************************************/

        /// <summary>Creates a new <see cref="UltEventsHelpUrlAttribute"/>.</summary>
        public UltEventsHelpUrlAttribute(Type type)
            : base(GetApiDocumentationUrl(type))
        { }

        /************************************************************************************************************************/

        private static readonly StringBuilder
            StringBuilder = new();

        /// <summary>Returns a URL for the given `type`'s API Documentation page.</summary>
        public static string GetApiDocumentationUrl(Type type)
            => GetApiDocumentationUrl(UltEventUtils.APIDocumentationURL, type);

        /// <summary>Returns a URL for the given `type`'s API Documentation page.</summary>
        public static string GetApiDocumentationUrl(string prefix, Type type)
        {
            StringBuilder.Length = 0;

            StringBuilder.Append(prefix);

            if (!string.IsNullOrEmpty(type.Namespace))
                StringBuilder.Append(type.Namespace).Append('/');

            StringBuilder.Append(type.Name.Replace('`', '_'));

            return StringBuilder.ToString();
        }

        /************************************************************************************************************************/
    }
}