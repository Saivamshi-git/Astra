using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices; // Required for NativeMethods P/Invoke
using System.Drawing;

namespace DesktopElementInspector
{
    /// <summary>
    /// A Data Transfer Object (DTO) that holds a snapshot of a UI element's properties.
    /// This is a lightweight, immutable record safe to pass to other parts of an application.
    /// </summary>


    public record DesktopScrapedElementDto(
        string DbId,
        string? Name,
        string ControlType,
        //string? AutomationId,
        //string? ClassName,
        //bool IsEnabled,
        Rectangle BoundingRectangle
        );

    public class TopWindowScraper
    {
        private const int MaxScanDepth = 25;
        private readonly AutomationBase _automation;

        // The cache holds the live, interactive AutomationElement objects.
        private readonly Dictionary<string, AutomationElement> _elementCache = new();
        private int _nextId = 0;

        private readonly HashSet<ControlType> _unimportantTypes = new()
            {
                ControlType.Pane,
                ControlType.Group,
                ControlType.Thumb,
                ControlType.Separator
            };

        public TopWindowScraper(AutomationBase automation)
        {
            _automation = automation;
        }



        /// <summary>
        /// Scrapes the foreground window and returns a list of DTOs representing the elements.
        /// Live elements are stored in an internal cache.
        /// </summary>
        public List<DesktopScrapedElementDto> Scrape()
        {
            // Clear previous results before starting a new scrape.
            _elementCache.Clear();
            _nextId = 0;
            var results = new List<DesktopScrapedElementDto>();

            var foregroundElement = FindForegroundElement();
            if (foregroundElement != null)
            {
                ScrapeElementRecursive(foregroundElement, 0, results);
            }

            return results;
        }

        /// <summary>
        /// Retrieves a cached, live AutomationElement by its temporary ID.
        /// It ensures the element is still available in the UI tree before returning it.
        /// </summary>
        /// <param name="elementId">The ID generated during the scrape (e.g., "twsa0").</param>
        /// <returns>The live AutomationElement or null if it's not found or no longer available.</returns>
        public AutomationElement? GetElementById(string elementId)
        {
            if (_elementCache.TryGetValue(elementId, out var element))
            {
                // Critical check: Ensure the UI element still exists before returning it.
                if (element.IsAvailable)
                {
                    return element;
                }
                // Clean up stale elements from the cache.
                _elementCache.Remove(elementId);
            }
            return null;
        }

        private AutomationElement? FindForegroundElement()
        {
            IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();
            if (foregroundWindowHandle == IntPtr.Zero) return null;
            // Retry is useful here as the handle might not be immediately available to automation.
            return Retry.WhileNull(() => _automation.FromHandle(foregroundWindowHandle), TimeSpan.FromSeconds(1)).Result;
        }

        private void ScrapeElementRecursive(AutomationElement element, int depth, List<DesktopScrapedElementDto> results)
        {
            if (depth >= MaxScanDepth || element == null || !element.IsAvailable) return;

            var p = element.Properties;
            var name = p.Name.ValueOrDefault;

            // --- The exact filtering logic from your original code ---
            bool isFiltered = string.IsNullOrEmpty(name) || (name != null && name.Length > 0 && !char.IsLetterOrDigit(name[0])) || _unimportantTypes.Contains(element.ControlType);

            // --- Only process and store the element if it's NOT filtered ---
            if (!isFiltered)
            {
                // 1. Generate a unique ID for this element.
                var elementId = $"dbsa{_nextId++}";

                // 2. Cache the live, interactive element.
                _elementCache[elementId] = element;

                // 3. Create the DTO with a snapshot of the element's data.
                var dto = new DesktopScrapedElementDto(
                    DbId: elementId,
                    Name: name,
                    ControlType: element.ControlType.ToString(),
                    //AutomationId: p.AutomationId.ValueOrDefault,
                    //ClassName: p.ClassName.ValueOrDefault,
                    //IsEnabled: p.IsEnabled.ValueOrDefault,
                    BoundingRectangle: p.BoundingRectangle.ValueOrDefault
);

                // 4. Add the DTO to our results list.
                results.Add(dto);
            }

            // --- ALWAYS traverse children, regardless of the parent's filter state ---
            var rawWalker = _automation.TreeWalkerFactory.GetRawViewWalker();
            try
            {
                // If the parent was filtered, its children start at the same "visual" depth.
                int nextDepth = isFiltered ? depth : depth + 1;

                var child = rawWalker.GetFirstChild(element);
                while (child != null)
                {
                    ScrapeElementRecursive(child, nextDepth, results);
                    // Important: The original element must be used to get the next sibling.
                    child = rawWalker.GetNextSibling(child);
                }
            }
            catch
            {
                // Catch errors from UI changes during the scan and stop traversing this branch.
            }
        }
        
                /* ------------------------ executors ----------------------------------------------*/
        public bool ExecuteClick(string DbId, string? expectedName)
        {
            // 1. Retrieve the element using our helper method.
            // This centrally handles checking if the element exists and is still available.
            var element = GetElementById(DbId);
            if (element == null)
            {
                System.Diagnostics.Debug.WriteLine($"Element with ID '{DbId}' not found or is no longer available.");
                return false;
            }

            // 2. Add a critical safety check.
            // Verifying the ClassName ensures we don't accidentally click the wrong
            // element if the UI structure has changed since the last scrape.
            if (element.Name != expectedName)
            {
                System.Diagnostics.Debug.WriteLine($"Element validation failed. Expected ClassName '{expectedName}' but found '{element.Name}'.");
                return false;
            }

            // 3. Perform the action.
            // UI interactions can fail for many reasons (e.g., the element is obscured
            // or disabled), so we wrap the call in a try-catch block for resilience.
            try
            {
                element.Focus();
                element.Click(moveMouse: true);

                return true;
            }
            catch (Exception ex)
            {
                // This will catch exceptions if the element becomes invalid right
                // before the click or if the click is blocked.
                System.Diagnostics.Debug.WriteLine($"Failed to click element '{DbId}'. Reason: {ex.Message}");
                return false;
            }
        }
    }


}