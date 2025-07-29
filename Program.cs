// Program.cs
using System;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace DesktopElementInspector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- Desktop Inspector ---");
            Console.WriteLine("1. Scan Top-Most Application Window (Full Detail)");
            Console.WriteLine("2. Scan Taskbar (Interactive Elements Only)");
            Console.WriteLine("---------------------------------------------");

            var inspector = new DesktopInspector();

            while (true)
            {
                Console.Write("\nEnter option (1 or 2) and press Enter: ");
                string? userInput = Console.ReadLine();

                try
                {
                    string? result = null;
                    switch (userInput)
                    {
                        case "1":
                            Console.WriteLine("\nScanning top application window...");
                            result = inspector.ScrapeTopWindow();
                            break;
                        case "2":
                            Console.WriteLine("\nScanning taskbar for interactive elements...");
                            result = inspector.ScrapeTaskbarInteractiveElements();
                            break;
                        default:
                            Console.WriteLine("Invalid option.");
                            continue;
                    }

                    if (!string.IsNullOrEmpty(result))
                    {
                        Console.WriteLine(result);
                    }
                    else
                    {
                        Console.WriteLine("Could not find or scrape the requested element(s).");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
    }

    /// <summary>
    /// A class dedicated to finding and scraping UI elements from the desktop.
    /// </summary>
    public class DesktopInspector
    {
        private const int MaxScanDepth = 15; // Increased depth for potentially nested taskbar items
        private readonly UIA3Automation _automation;

        public DesktopInspector()
        {
            _automation = new UIA3Automation();
        }

        /// <summary>
        /// Finds the top-most application window and scrapes its entire element tree with full detail.
        /// </summary>
        /// <returns>A string containing the formatted tree of UI elements, or null if no window is found.</returns>
        public string? ScrapeTopWindow()
        {
            IntPtr windowHandle = FindTopWindowHandle();
            if (windowHandle == IntPtr.Zero) return null;

            var topElement = _automation.FromHandle(windowHandle);
            var sb = new StringBuilder();
            sb.AppendLine("\n================== Top Window Found ==================");
            ScrapeElementRecursive(topElement, 0, sb);
            sb.AppendLine("\n==================== Scan Complete ===================");
            return sb.ToString();
        }

        /// <summary>
        /// Finds the Windows Taskbar and scrapes only its interactive elements with curated details.
        /// </summary>
        /// <returns>A formatted string listing only the interactive elements, or null if the taskbar is not found.</returns>
        public string? ScrapeTaskbarInteractiveElements()
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
        /// NEW: Recursively finds and formats only interactive elements (like Buttons).
        /// </summary>
        private void ScrapeInteractiveElementsRecursive(AutomationElement element, int depth, StringBuilder sb)
        {
            if (depth >= MaxScanDepth || !element.IsAvailable || element.Properties.IsOffscreen.ValueOrDefault)
            {
                return;
            }

            // We only record the element if it's a Button.
            if (element.ControlType == ControlType.Button)
            {
                var p = element.Properties;
                string name = GetSafePropertyValue(p.Name);

                // Skip buttons with no discernible name, as they are likely separators or placeholders.
                if (!string.IsNullOrEmpty(name) && name != "null")
                {
                    sb.AppendLine("---");
                    sb.AppendLine($"  Name:               '{name}'");
                    sb.AppendLine($"  ControlType:        {element.ControlType}");
                    sb.AppendLine($"  Process ID:         {GetSafePropertyValue(p.ProcessId)}");
                    sb.AppendLine($"  Bounding Rectangle: {GetSafePropertyValue(p.BoundingRectangle)}");
                }
            }

            // Always recurse into children, regardless of the parent's type, to find nested buttons.
            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeInteractiveElementsRecursive(child, depth + 1, sb);
                }
            }
            catch { /* Ignore errors if UI changes during scan */ }
        }

        /// <summary>
        /// Original scraper: Recursively builds a string representation of an element and all its children with full detail.
        /// </summary>
        private void ScrapeElementRecursive(AutomationElement element, int depth, StringBuilder sb)
        {
            // This is the original, detailed scraper used for the top-level window.
            if (depth >= MaxScanDepth || !element.IsAvailable || element.Properties.IsOffscreen.ValueOrDefault) return;
            var boundingRectProperty = element.Properties.BoundingRectangle;
            if (!boundingRectProperty.IsSupported || boundingRectProperty.Value.IsEmpty) return;

            string indent = new string(' ', depth * 4);
            sb.AppendLine($"{indent}---");
            var p = element.Properties;
            sb.AppendLine($"{indent}  Name:               '{GetSafePropertyValue(p.Name)}'");
            sb.AppendLine($"{indent}  AutomationId:       '{GetSafePropertyValue(p.AutomationId)}'");
            sb.AppendLine($"{indent}  ControlType:        {element.ControlType}");
            sb.AppendLine($"{indent}  ClassName:          '{GetSafePropertyValue(p.ClassName)}'");
            sb.AppendLine($"{indent}  IsEnabled:          {GetSafePropertyValue(p.IsEnabled)}");
            sb.AppendLine($"{indent}  Bounding Rectangle: {GetSafePropertyValue(p.BoundingRectangle)}");

            try
            {
                foreach (var child in element.FindAllChildren())
                {
                    ScrapeElementRecursive(child, depth + 1, sb);
                }
            }
            catch { /* Ignore */ }
        }

        private IntPtr FindTopWindowHandle()
        {
            IntPtr currentHandle = NativeMethods.GetTopWindow(IntPtr.Zero);
            while (currentHandle != IntPtr.Zero)
            {
                if (IsRealWindow(currentHandle)) return currentHandle;
                currentHandle = NativeMethods.GetWindow(currentHandle, NativeMethods.GW_HWNDNEXT);
            }
            return IntPtr.Zero;
        }

        private bool IsRealWindow(IntPtr handle)
        {
            if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.GetWindowTextLength(handle) == 0) return false;
            int extendedStyle = (int)NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
            if ((extendedStyle & NativeMethods.WS_EX_TOOLWINDOW) == NativeMethods.WS_EX_TOOLWINDOW) return false;
            if (!NativeMethods.GetWindowRect(handle, out var rect) || rect.Width < 100 || rect.Height < 100) return false;
            return true;
        }

        private string GetSafePropertyValue<T>(AutomationProperty<T> property)
        {
            if (!property.IsSupported) return "[Not Supported]";
            T value = property.ValueOrDefault;
            return value?.ToString() ?? "null";
        }
    }

    internal static class NativeMethods
    {
        internal const uint GW_HWNDNEXT = 2;
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left; public int Top; public int Right; public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }
    }
}
