using FlaUI.Core;

namespace DesktopElementInspector
{
    /// <summary>
    /// Provides shared utility methods for working with FlaUI.
    /// </summary>
    public static class AutomationUtils
    {
        /// <summary>
        /// Safely retrieves the value of an automation property, handling cases where the
        /// property is not supported or its value is null.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="property">The FlaUI automation property to access.</param>
        /// <returns>A string representation of the value, or a status message.</returns>
        public static string GetSafePropertyValue<T>(AutomationProperty<T> property)
        {
            if (!property.IsSupported) return "[Not Supported]";
            T value = property.ValueOrDefault;
            return value == null ? "null" : (value.ToString() ?? string.Empty);
        }
    }
}
