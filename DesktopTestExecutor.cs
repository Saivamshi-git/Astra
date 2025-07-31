using FlaUI.Core;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;

namespace DesktopElementInspector
{
    /// <summary>
    /// Provides a self-contained, interactive console test for the TaskbarScraper.
    /// </summary>
    public static class DesktopTestExecutor
    {
        /// <summary>
        /// Runs the interactive test session for scraping and clicking taskbar elements.
        /// </summary>
        public static void RunRecursiveTest()
        {
            // Using a using declaration for the automation object ensures it's
            // properly disposed of even if errors occur.
            using var automation = new UIA3Automation();
            var DesktopScraper = new TopWindowScraper(automation);

            Console.WriteLine("--- Starting Taskbar Element Test ---");
            Console.WriteLine("Scanning taskbar elements...");
            var scrapedElements = DesktopScraper.Scrape();

            if (scrapedElements.Count == 0)
            {
                Console.WriteLine("No clickable button elements were found on the taskbar.");
                Console.WriteLine("Press any key to exit test.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nFound the following taskbar buttons:");
            for (int i = 0; i < scrapedElements.Count; i++)
            {
                var element = scrapedElements[i];
                Console.WriteLine($"  [{i}] ID: {element.DbId}, Name: {element.Name}");
            }

            // Main input loop
            while (true)
            {
                Console.WriteLine("\nEnter the number of the element to click (or type 'exit' to quit):");
                var input = Console.ReadLine();

                if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!int.TryParse(input, out int selection) || selection < 0 || selection >= scrapedElements.Count)
                {
                    Console.WriteLine("Invalid input. Please enter a number from the list above.");
                    continue;
                }

                var selectedElement = scrapedElements[selection];
                Console.WriteLine($"\nAttempting to click element [{selection}]: '{selectedElement.Name}'...");

                bool success = DesktopScraper.ExecuteClick(selectedElement.DbId, selectedElement.Name);

                if (success)
                {
                    Console.WriteLine("=> Click executed successfully.");
                }
                else
                {
                    Console.WriteLine("=> Click failed. The element might no longer be available or was blocked.");
                    Console.WriteLine("-----------------------------------ressraping---------------------------------------------------------------------------");


                    RunRecursiveTest();

                }
            }

            Console.WriteLine("--- Exiting Taskbar Element Test ---");
        }
    }
}
