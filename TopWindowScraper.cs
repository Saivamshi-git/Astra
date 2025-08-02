using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using System;
using System.Collections.Generic;

namespace DesktopElementInspector
{
    public record DesktopScrapedElementDto(
        string DbId,
        string? Name,
        string ControlType,
        string? ClassName
    );

    /// <summary>
    /// Scrapes all UI elements from the foreground application window.
    /// This version uses minimal filtering to be as exhaustive as possible.
    /// </summary>
    public class TopWindowScraper
    {
        private readonly Dictionary<string, AutomationElement> _elementCache = new();
        private readonly AutomationBase _automation;
        private int _nextId = 0;
        private const int MaxScanDepth = 30; // Using a deep scan depth by default

        public TopWindowScraper(AutomationBase automation)
        {
            _automation = automation;
        }

        /// <summary>
        /// Scrapes the foreground window exhaustively.
        /// </summary>
        public List<DesktopScrapedElementDto> ScrapeForegroundWindow()
        {
            _elementCache.Clear();
            _nextId = 0;
            var results = new List<DesktopScrapedElementDto>();

            var foregroundElement = FindForegroundElement();
            if (foregroundElement == null) return results;

            ScrapeElementRecursive(foregroundElement, 0, results);
            return results;
        }

        /// <summary>
        /// Recursively scrapes an element and its children with minimal filtering.
        /// </summary>
        private void ScrapeElementRecursive(AutomationElement element, int depth, List<DesktopScrapedElementDto> results)
        {
            // Only the most essential guards remain to prevent errors and infinite loops.
            // All other filters (IsOffscreen, BoundingRectangle) have been removed
            // to ensure we capture every single element the automation framework can see.
            if (depth >= MaxScanDepth || !element.IsAvailable) return;

            var dbId = $"dbsa{_nextId++}";
            _elementCache[dbId] = element;
            results.Add(new DesktopScrapedElementDto(
                DbId: dbId,
                Name: element.Name,
                ControlType: element.ControlType.ToString(),
                ClassName: element.ClassName
            ));

            try
            {
                // Recurse into children
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeElementRecursive(child, depth + 1, results);
                }
            }
            catch
            { 
                // Ignore errors if the UI changes during the scan
            }
        }

        /// <summary>
        /// Finds the element for the foreground window using the original, robust logic.
        /// </summary>
        private AutomationElement? FindForegroundElement()
        {
            IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();
            if (foregroundWindowHandle == IntPtr.Zero) return null;

            var desktop = _automation.GetDesktop();
            if (desktop == null) return null;

            foreach (var child in desktop.FindAllChildren())
            {
                if (child.Properties.NativeWindowHandle.ValueOrDefault == foregroundWindowHandle)
                {
                    return child;
                }
            }

            try
            {
                return _automation.FromHandle(foregroundWindowHandle);
            }
            catch
            {
                return null;
            }
        }
        
        public AutomationElement? GetElementById(string tempId)
        {
            if (_elementCache.TryGetValue(tempId, out var element) && element.IsAvailable)
            {
                return element;
            }
            return null;
        }

        public bool ExecuteClick(string dbId, string? expectedClassName)
        {
            var element = GetElementById(dbId);
            if (element == null)
            {
                System.Diagnostics.Debug.WriteLine($"Element with ID '{dbId}' not found or no longer available.");
                return false;
            }

            if (element.ClassName != expectedClassName)
            {
                System.Diagnostics.Debug.WriteLine($"Element validation failed. Expected ClassName '{expectedClassName}' but found '{element.ClassName}'.");
                return false;
            }
            
            try
            {
                element.Click(moveMouse: true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to click element '{dbId}'. Reason: {ex.Message}");
                return false;
            }
        }
    }
}
