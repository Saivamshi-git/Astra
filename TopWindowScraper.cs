using System;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DesktopElementInspector
{
    /// <summary>
    /// A class dedicated to finding and scraping the UI element tree of the foreground window.
    /// </summary>
    public class TopWindowScraper
    {
        private const int MaxScanDepth = 15;
        private readonly AutomationBase _automation;

        public TopWindowScraper(AutomationBase automation)
        {
            _automation = automation;
        }

        /// <summary>
        /// Finds the foreground window and scrapes its entire element tree with full detail.
        /// </summary>
        /// <returns>A string containing the formatted tree of UI elements, or null if no window is found.</returns>
        public string? Scrape()
        {
            var foregroundElement = FindForegroundElement();
            if (foregroundElement == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("\n================== Foreground Window Found ==================");
            ScrapeElementRecursive(foregroundElement, 0, sb);
            sb.AppendLine("\n======================= Scan Complete =======================");
            return sb.ToString();
        }

        /// <summary>
        /// Finds the element corresponding to the current foreground window.
        /// This method is reliable for both standard applications and modern UI flyouts.
        /// </summary>
        /// <returns>The AutomationElement for the foreground window, or null if it cannot be found.</returns>
        private AutomationElement? FindForegroundElement()
        {
            // Get the handle of the window that the user is currently working with.
            IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();
            if (foregroundWindowHandle == IntPtr.Zero) return null;

            // Get the root desktop element from FlaUI. All top-level windows are children of the desktop.
            var desktop = _automation.GetDesktop();
            if (desktop == null) return null;

            // Find the specific child of the desktop that matches the foreground window handle.
            // This is more reliable than Z-order walking as it uses the UI Automation tree.
            foreach (var child in desktop.FindAllChildren())
            {
                if (child.Properties.NativeWindowHandle.ValueOrDefault == foregroundWindowHandle)
                {
                    return child;
                }
            }

            // Fallback for cases where the foreground window is not a direct child of the desktop
            // (e.g., some complex applications). We can get the element directly from the handle.
            try
            {
                return _automation.FromHandle(foregroundWindowHandle);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recursively builds a string representation of an element and all its children with full detail.
        /// </summary>
        private void ScrapeElementRecursive(AutomationElement element, int depth, StringBuilder sb)
        {
            if (depth >= MaxScanDepth || !element.IsAvailable || element.Properties.IsOffscreen.ValueOrDefault) return;
            
            var boundingRectProperty = element.Properties.BoundingRectangle;
            if (!boundingRectProperty.IsSupported || boundingRectProperty.Value.IsEmpty) return;

            string indent = new string(' ', depth * 4);
            sb.AppendLine($"{indent}---");
            var p = element.Properties;
            sb.AppendLine($"{indent}  Name:              '{AutomationUtils.GetSafePropertyValue(p.Name)}'");
            sb.AppendLine($"{indent}  ControlType:       {element.ControlType}");
            sb.AppendLine($"{indent}  IsEnabled:         {AutomationUtils.GetSafePropertyValue(p.IsEnabled)}");

            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeElementRecursive(child, depth + 1, sb);
                }
            }
            catch
            {
                // Ignore errors if the UI changes during the scan, which is common.
            }
        }
    }
}
