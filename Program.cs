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
            Console.WriteLine("3. test taskbar (Interactive Elements Only)");
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
                            Console.WriteLine("\nScanning Desktop for recursive elements...");
                            Console.WriteLine("=======================================================================");
                            Thread.Sleep(5000);
                            Console.WriteLine("==================waiting is done===================");

                            // result = topWindowScraper.Scrape();
                            var DscrapedElements = topWindowScraper.AnalyzeSemantically();
                            // var res = topWindowScraper.Scrape();
                            //res.ForEach(item => Console.WriteLine(item));

                            // Use string.Join and a LINQ Select to format each element's data.
                            // topWindowScraper.PrintTree(DscrapedElements);
                            topWindowScraper.PrintSemanticView(DscrapedElements);
                            // var Doutput = string.Join(",\n  ", DscrapedElements.Select(dto =>
                            //     $"{{ Parent name: \"{dto.ParentName}\" , DbId: \"{dto.DbId}\", Name: \"{dto.Name}\",classname: \"{dto.ClassName}\", ControlType: \"{dto.ControlType}\", }}"
                            // ));
                            //\" ,Boundings : \"{dto.BoundingRectangle}\"  
                            //Print the final combined string.
                            // Console.WriteLine("[\n  " + Doutput + "\n]");
                            Console.WriteLine("=======================================================================");
                            break;
                        case "2":
                            Console.WriteLine("\nScanning taskbar for interactive elements...");
                            Console.WriteLine("=======================================================================");
                            var scrapedElements = taskbarScraper.ScrapeAndCache();
                            // Use string.Join and a LINQ Select to format each element's data.
                            var output = string.Join(",\n  ", scrapedElements.Select(dto =>
                                $"{{ TbId: \"{dto.TbId}\", Name: \"{dto.Name}\", ControlType: \"{dto.ControlType}\" , Boundings : \"{dto.BoundingRectangle}\"}}"
                            ));
                            // Print the final combined string.
                            Console.WriteLine("[\n  " + output + "\n]");
                            Console.WriteLine("=======================================================================");
                            break;
                        case "3":
                              // To run the taskbar test, just make this single method call:
                            TaskbarTestExecutor.RunInteractiveTest();

                            Console.WriteLine("The taskbar test has completed. Resuming main application flow.");
                            break;
                        case "4":


                            //To run the taskbar test, just make this single method call:
                            // DesktopTestExecutor.RunRecursiveTest();

                            Console.WriteLine("\nProgram finished.");
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
