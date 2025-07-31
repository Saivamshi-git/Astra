using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;

namespace DesktopElementInspector
{
    public record ScrapedElementDto(
        string TbId,
        string? Name,
        string ControlType,
        //string? AutomationId,
        //string? ClassName,
        //bool IsEnabled,
        Rectangle BoundingRectangle
    );

    public class TaskbarScraper
    {
        private const int MaxScanDepth = 15;
        private readonly Dictionary<string, AutomationElement> _elementCache = new();
        private readonly AutomationBase _automation;
        private int _nextId = 0;

        public TaskbarScraper(AutomationBase automation)
        {
            _automation = automation;
        }

        public List<ScrapedElementDto> ScrapeAndCache()
        {
            _elementCache.Clear();
            _nextId = 0;

            var results = new List<ScrapedElementDto>();
            var taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) return results;

            var taskbarElement = _automation.FromHandle(taskbarHandle);
            var processedElements = new HashSet<AutomationElement>();
            ScrapeRecursive(taskbarElement, 0, results, processedElements);
            return results;
        }

        private void ScrapeRecursive(AutomationElement element, int depth, List<ScrapedElementDto> results, HashSet<AutomationElement> processed)
        {
            if (depth >= MaxScanDepth || !element.IsAvailable || element.Properties.IsOffscreen.ValueOrDefault) return;
            if (!processed.Add(element)) return;

            var p = element.Properties;
            var name = p.Name.ValueOrDefault;

            if (element.ControlType == ControlType.Button && !string.IsNullOrEmpty(element.Name))
            {
                // Use string interpolation to add the "tbsa" prefix.
                var tbId = $"tbsa{_nextId++}";
                _elementCache[tbId] = element;

                var elementDto = new ScrapedElementDto(
                    TbId: tbId,
                    Name: name,
                    ControlType: element.ControlType.ToString(),
                    //AutomationId: p.AutomationId.ValueOrDefault,
                    //ClassName: p.ClassName.ValueOrDefault,
                    //IsEnabled: p.IsEnabled.ValueOrDefault,
                    BoundingRectangle: p.BoundingRectangle.ValueOrDefault
                );
                results.Add(elementDto);
            }

            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeRecursive(child, depth + 1, results, processed);
                }
            }
            catch { /* Ignore UI changes during scan */ }
        }

        public AutomationElement? GetElementById(string tempId)
        {
            if (_elementCache.TryGetValue(tempId, out var element))
            {
                if (element.IsAvailable)
                {
                    return element;
                }
                _elementCache.Remove(tempId);
            }
            return null;
        }
        
        /* ------------------------ executors ----------------------------------------------*/
        public bool ExecuteClick(string tbId, string? expectedName)
        {
            // 1. Retrieve the element using our helper method.
            // This centrally handles checking if the element exists and is still available.
            var element = GetElementById(tbId);
            if (element == null)
            {
                System.Diagnostics.Debug.WriteLine($"Element with ID '{tbId}' not found or is no longer available.");
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
                element.Click();
                return true;
            }
            catch (Exception ex)
            {
                // This will catch exceptions if the element becomes invalid right
                // before the click or if the click is blocked.
                System.Diagnostics.Debug.WriteLine($"Failed to click element '{tbId}'. Reason: {ex.Message}");
                return false;
            }
        }

    }
}