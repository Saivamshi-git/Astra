using System;
using FlaUI.UIA3;
using System.Threading;

namespace DesktopElementInspector
{
    /// <summary>
    /// Main entry point for the Desktop Element Inspector application.
    /// Handles user input and delegates scraping tasks to specialized classes.
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("--- Desktop Inspector ---");
            Console.WriteLine("1. Scan Top-Most Application Window (Full Detail)");
            Console.WriteLine("2. Scan Taskbar (Interactive Elements Only)");
            Console.WriteLine("---------------------------------------------");

            // UIA3Automation is the main entry point for FlaUI.
            // It's best to create it once and reuse it.
            using var automation = new UIA3Automation();
            var topWindowScraper = new TopWindowScraper(automation);
            var taskbarScraper = new TaskbarScraper(automation);

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
                            Thread.Sleep(3000);
                            result = topWindowScraper.Scrape();
                            break;
                        case "2":
                            Console.WriteLine("\nScanning taskbar for interactive elements...");
                            result = taskbarScraper.Scrape();
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
}
