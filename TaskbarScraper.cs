using System;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DesktopElementInspector
{
    /// <summary>
    /// A class dedicated to finding and scraping interactive UI elements from the Windows Taskbar.
    /// </summary>
    public class TaskbarScraper
    {
        private const int MaxScanDepth = 15;
        private readonly AutomationBase _automation;

        public TaskbarScraper(AutomationBase automation)
        {
            _automation = automation;
        }

        /// <summary>
        /// Finds the Windows Taskbar and scrapes only its interactive elements with curated details.
        /// </summary>
        /// <returns>A formatted string listing only the interactive elements, or null if the taskbar is not found.</returns>
        public string? Scrape()
        {
            IntPtr taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) return null;
            
            var taskbarElement = _automation.FromHandle(taskbarHandle);
            var sb = new StringBuilder();
            sb.AppendLine("\n========== Interactive Taskbar Elements ==========");
            ScrapeInteractiveElementsRecursive(taskbarElement, 0, sb);
            sb.AppendLine("================================================");
            return sb.ToString();
        }

        /// <summary>
        /// Recursively finds and formats only interactive elements (like Buttons).
        /// </summary>
        private void ScrapeInteractiveElementsRecursive(AutomationElement element, int depth, StringBuilder sb)
        {
            if (depth >= MaxScanDepth || !element.IsAvailable || element.Properties.IsOffscreen.ValueOrDefault)
            {
                return;
            }

            // We are only interested in specific interactive types, primarily buttons for the taskbar.
            if (element.ControlType == ControlType.Button)
            {
                var p = element.Properties;
                string name = AutomationUtils.GetSafePropertyValue(p.Name);

                if (!string.IsNullOrEmpty(name) && name != "null")
                {
                    sb.AppendLine("---");
                    sb.AppendLine($"  Name:              '{name.Trim()}'");
                    sb.AppendLine($"  ControlType:       {element.ControlType}");
                }
            }

            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeInteractiveElementsRecursive(child, depth + 1, sb);
                }
            }
            catch 
            { 
                // Ignore errors if UI changes during scan
            }
        }
    }
}
