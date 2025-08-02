using FlaUI.Core;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using FlaUI.Core.Capturing;
using System.IO;
using System.Linq;

namespace DesktopElementInspector
{
    public static class DesktopTestExecutor
    {
        public static void RunRecursiveTest()
        {
            using var automation = new UIA3Automation();
            var desktopScraper = new TopWindowScraper(automation);
            var failedClickDtos = new List<DesktopScrapedElementDto>();
            // A new list to hold elements we shouldn't click immediately.
            var deferredCloseElements = new List<DesktopScrapedElementDto>();

            Console.WriteLine("--- Starting Automated UI Element Test ---");
            Console.WriteLine("Please bring the target window to the foreground. Scanning will begin in 5 seconds...");
            System.Threading.Thread.Sleep(5000);

            Console.WriteLine("\nScanning foreground window elements...");
            var scrapedElements = desktopScraper.Scrape();

            if (scrapedElements.Count == 0)
            {
                Console.WriteLine("No actionable elements were found in the foreground window.");
                Console.WriteLine("Press any key to exit test.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"\nFound {scrapedElements.Count} elements. Testing all non-destructive elements first...");

            // 1. Traverse and click, but defer "close" buttons.
            foreach (var elementDto in scrapedElements)
            {
                // Defer destructive elements to a separate list.
                if (IsCloseElement(elementDto))
                {
                    deferredCloseElements.Add(elementDto);
                    continue; // Skip clicking for now.
                }

                Console.Write($"  -> Clicking '{elementDto.Name}' (ID: {elementDto.DbId})... ");
                bool success = desktopScraper.ExecuteClick(elementDto.DbId, elementDto.Name);

                if (success)
                {
                    Console.WriteLine("[SUCCESS]");
                }
                else
                {
                    Console.WriteLine("[FAILED]");
                    failedClickDtos.Add(elementDto);
                }
                System.Threading.Thread.Sleep(250);
            }

            // 2. Handle the standard failures first.
            if (failedClickDtos.Count > 0)
            {
                HandleFailures(desktopScraper, failedClickDtos);
            }
            else
            {
                Console.WriteLine("\n✅ --- All non-destructive elements were clicked successfully! ---");
            }
            
            // 3. Now, handle the deferred close buttons with user interaction.
            if(deferredCloseElements.Count > 0)
            {
                HandleDeferredCloseElements(desktopScraper, deferredCloseElements);
            }

            Console.WriteLine("\n--- Test complete. Press any key to exit. ---");
            Console.ReadKey();
        }

        /// <summary>
        /// Checks if an element is likely a "close" button based on its name and type.
        /// </summary>
        private static bool IsCloseElement(DesktopScrapedElementDto dto)
        {
            if (dto.Name is null || dto.ControlType != "Button")
            {
                return false;
            }
            // Use case-insensitive comparison for robustness.
            string lowerCaseName = dto.Name.ToLowerInvariant();
            return lowerCaseName.Contains("close") || lowerCaseName.Contains("exit") || lowerCaseName.Contains("close tab") ;
        }


        // In DesktopTestExecutor.cs

        /// <summary>
        /// Handles reporting failed elements and then highlighting them using a custom overlay window
        /// on a dedicated UI thread.
        /// </summary>
        private static void HandleFailures(TopWindowScraper scraper, List<DesktopScrapedElementDto> failedDtos)
        {
            Console.WriteLine($"\n❌ --- {failedDtos.Count} Click(s) Failed ---");

            var rectanglesToHighlight = new List<System.Drawing.Rectangle>();
            foreach (var failedDto in failedDtos)
            {
                var element = scraper.GetElementById(failedDto.DbId);
                if (element != null)
                {
                    var rect = element.Properties.BoundingRectangle.ValueOrDefault;
                    if (rect != default)
                    {
                        rectanglesToHighlight.Add(rect);
                    }
                }
            }

            Console.WriteLine("\n--- Failed Element Report ---");
            foreach (var failedDto in failedDtos)
            {
                Console.WriteLine($"dbid:{failedDto.DbId}, name:{failedDto.Name}, controltype:{failedDto.ControlType}");
            }

            if (rectanglesToHighlight.Any())
            {
                Console.WriteLine($"\nHighlighting the {rectanglesToHighlight.Count} failed element(s) that have a visual location...");

                // --- FIX: Run the Form on a dedicated UI thread ---
                HighlightWindow? highlightForm = null;
                var uiThread = new Thread(() =>
                {
                    highlightForm = new HighlightWindow(rectanglesToHighlight);
                    // Application.Run starts the message loop for the form on this new thread.
                    System.Windows.Forms.Application.Run(highlightForm);
                });

                // WinForms requires a Single-Threaded Apartment model.
                uiThread.SetApartmentState(ApartmentState.STA);
                uiThread.Start();

                Console.WriteLine("\n🔴 Highlights are active. Type 'stop' and press Enter to remove them and continue.");
                while (!string.Equals(Console.ReadLine(), "stop", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Invalid command. Type 'stop' to continue.");
                }

                // Safely close the form from our main thread by invoking the action on the UI thread.
                if (highlightForm != null)
                {
                    highlightForm.Invoke(highlightForm.Close);
                }

                Console.WriteLine("Highlights removed.");
            }
            else
            {
                Console.WriteLine("\nNone of the failed elements have a visual location to highlight.");
            }
        }   
        /// <summary>
        /// Prompts the user before attempting to click potentially destructive "close" buttons.
        /// </summary>
        private static void HandleDeferredCloseElements(TopWindowScraper scraper, List<DesktopScrapedElementDto> closeElements)
        {
            Console.WriteLine("\n-------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n⚠️ Identified {closeElements.Count} potentially destructive 'close' button(s).");
            Console.WriteLine("These will be clicked next.");
            Console.ResetColor();
            
            Console.WriteLine("\nTo prevent closing your main application window, please prepare a safe target.");
            Console.WriteLine("(For example, open an extra browser tab or a dummy window).");
            Console.Write("\nPress Enter when you are ready to proceed...");
            Console.ReadLine();

            Console.WriteLine("\nProceeding with 'close' button clicks...");
            int successCount = 0;
            foreach(var elementDto in closeElements)
            {
                Console.Write($"  -> Clicking deferred element '{elementDto.Name}' (ID: {elementDto.DbId})... ");
                bool success = scraper.ExecuteClick(elementDto.DbId, elementDto.Name);
                if (success)
                {
                    Console.WriteLine("[SUCCESS]");
                    successCount++;
                }
                else
                {
                    Console.WriteLine("[FAILED]");
                }
                System.Threading.Thread.Sleep(500);
            }
            Console.WriteLine($"\n--- Close Action Summary: {successCount} of {closeElements.Count} buttons clicked successfully. ---");
        }
    }
}