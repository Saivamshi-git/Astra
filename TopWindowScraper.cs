    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using FlaUI.Core;
    using FlaUI.Core.AutomationElements;
    using FlaUI.Core.Conditions;
    using FlaUI.Core.Definitions;
    


    // The DTO record remains the same
public record DesktopScrapedElementDto(
    string DbId,
    string? Name,
    string? ParentDbId,
    string ControlType,
    string? ClassName,
    string? LandmarkType,
    string? ParentName,
    System.Drawing.Rectangle BoundingRectangle

);

    namespace DesktopElementInspector
    {
        // ===================================================================
        // CORE DATA STRUCTURES
        // ===================================================================

        public class TreeNode
        {
            public DesktopScrapedElementDto Data { get; set; }
            public List<TreeNode> Children { get; set; }
            public TreeNode? Parent { get; set; }

        public TreeNode(DesktopScrapedElementDto data)
        {
            Data = data;
            Children = new List<TreeNode>();
            Parent = null;
        }
        }

        public class SemanticViewComponent
        {
            public string ComponentName { get; set; }
            public string ComponentType { get; set; }

            public List<TreeNode> RootNodes { get; set; }

            public SemanticViewComponent(string name,  string type,List<TreeNode> nodes)
            {
                ComponentName = name;
                ComponentType = type;
                RootNodes = nodes;
            }
        }

        public record SemanticRule(string ComponentName, Func<TreeNode, bool> Predicate, int Priority, string ComponentType = "Variable");

        
        public enum ReactionType
        {
            Refresh,    // For in-place component updates.
            Temporary,  // For menus and dialogs.
            Diff        // For property-level state changes.
        }

        public record ReactionRule(ReactionType Type, List<string> TargetComponents);



        // ===================================================================
        // EXTENSION METHODS
        // ===================================================================

        public static class TreeNodeExtensions
        {
            public static List<TreeNode> FindAllNodesInTree(this TreeNode startNode, Func<TreeNode, bool> predicate)
            {
                var foundNodes = new List<TreeNode>();
                var queue = new Queue<TreeNode>();
                queue.Enqueue(startNode);
                while (queue.Count > 0)
                {
                    var currentNode = queue.Dequeue();
                    if (predicate(currentNode))
                    {
                        foundNodes.Add(currentNode);
                    }
                    foreach (var child in currentNode.Children)
                    {
                        queue.Enqueue(child);
                    }
                }
                return foundNodes;
            }

            public static TreeNode? FindNodeInTree(this TreeNode currentNode, Func<TreeNode, bool> predicate)
            {
                if (predicate(currentNode)) return currentNode;
                foreach (var child in currentNode.Children)
                {
                    var result = FindNodeInTree(child, predicate);
                    if (result != null) return result;
                }
                return null;
            }
        }

        // ===================================================================
        // RULE PROVIDER ARCHITECTURE
        // ===================================================================

         // ===================================================================
        //  Semantic Rules
        // ===================================================================

        public interface ISemanticRuleProvider
        {
            List<SemanticRule> GetRules(TreeNode windowNode);
        }

        public class FileExplorerRuleProvider : ISemanticRuleProvider
        {
            public List<SemanticRule> GetRules(TreeNode windowNode)
            {
                var rules = new List<SemanticRule>();
                var landmarkGroups = windowNode.FindAllNodesInTree(node => HasMeaningfulLandmark(node))
                    .GroupBy(node => node.Data.LandmarkType!);

                foreach (var group in landmarkGroups)
                {
                    string componentName = group.Key.Equals("Navigation", StringComparison.OrdinalIgnoreCase)
                        ? "Address Bar (Breadcrumb)"
                        : $"{char.ToUpper(group.Key[0])}{group.Key.Substring(1)} Landmark";
                    rules.Add(new SemanticRule(componentName, node => group.Contains(node), 10));
                }

                rules.AddRange(new[]
                {
                    //static
                    new SemanticRule("Title Bar", node => node.Data.ControlType == "TitleBar", 100, "Static"),
                    new SemanticRule("Address Bar (Root Item)", node => node.Data.ClassName == "FileExplorerExtensions.FirstCrumbStackPanelControl", 91, "Static"),
                    new SemanticRule("Command Bar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Cut" || n.Data.Name == "View") != null, 85, "Static"),
                    new SemanticRule("Navigation Toolbar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Back") != null, 84, "Static"),
                    new SemanticRule("Status Bar", node => node.Data.ControlType == "StatusBar", 80, "Static"),
                    new SemanticRule("Details Bar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Details") != null, 83, "Static"),
                    //variable
                    new SemanticRule("Tab Bar", node => node.Data.ClassName == "Microsoft.UI.Xaml.Controls.TabView", 95, "Variable"),
                    new SemanticRule("Address Bar (Input)", node => node.Data.ClassName == "AutoSuggestBox", 90, "Variable"),
                    new SemanticRule("Navigation Pane", node => node.Data.Name == "Navigation Pane" && node.Data.ControlType == "Tree", 50, "Variable"),
                    new SemanticRule("Main Content", node => node.Data.Name == "Items View" && node.Data.ControlType == "List", 49, "Variable")
                });

                return rules.OrderByDescending(r => r.Priority).ToList();
            }

            private bool HasMeaningfulLandmark(TreeNode node)
            {
                var landmarkType = node.Data.LandmarkType;
                return !string.IsNullOrEmpty(landmarkType) && !landmarkType.Equals("None", StringComparison.OrdinalIgnoreCase) && landmarkType != "0";
            }
        }
        public class VSCodeRuleProvider : ISemanticRuleProvider
        {
            public List<SemanticRule> GetRules(TreeNode windowNode)
            {
                var rules = new List<SemanticRule>();
                rules.AddRange(new[]
                {
                    //static
                    new SemanticRule("Window Controls", n => n.Data.ControlType == "Button" && (n.Data.Name == "Minimize" || n.Data.Name == "Restore" || n.Data.Name == "Close"), 100, "Static"),
                    new SemanticRule("Menu Bar", n => n.Data.ControlType == "MenuBar" && n.FindNodeInTree(c => c.Data.Name == "File") != null, 95, "Static"),
                    new SemanticRule("Main Toolbar", n => n.Data.ControlType == "ToolBar" && n.FindNodeInTree(c => c.Data.Name != null && c.Data.Name.StartsWith("Go Back")) != null, 90, "Static"),
                    new SemanticRule("Layout Controls", n => n.Data.Name == "Title actions" && n.Data.ControlType == "ToolBar", 88, "Static"),
                    new SemanticRule("Status Bar", n => n.Data.ControlType == "StatusBar", 50, "Static"),
                    new SemanticRule("Editor Actions", n => n.Data.Name == "Editor actions" && n.Data.ControlType == "ToolBar", 72, "Static"),
                    new SemanticRule("Account & Settings Bar", n => n.Data.ControlType == "ToolBar" && n.FindNodeInTree(c => c.Data.Name == "Accounts" || c.Data.Name == "Manage") != null, 84, "Static"),
                    //variable
                    new SemanticRule("Side Bar", n => n.Data.ControlType == "Tree" && n.Data.Name == "Files Explorer", 80, "Variable"),
                    new SemanticRule("Activity Bar", n => n.Data.Name == "Active View Switcher" && n.Data.ControlType == "Tab", 85, "Variable"),
                    new SemanticRule("Editor Tabs", n => n.Data.ControlType == "Tab" && n.FindNodeInTree(c => c.Data.ControlType == "TabItem") != null && n.FindNodeInTree(c => c.Data.Name == "Active View Switcher") == null, 71, "Variable"),
                    new SemanticRule("Editor Group", n => n.Data.ControlType == "Edit" && n.Parent != null  && n.Parent.Data.LandmarkType != null && n.Parent.Data.LandmarkType.StartsWith("Main") && (n.Data.BoundingRectangle.Width * n.Data.BoundingRectangle.Height > 40000), 70, "Variable"),
                    new SemanticRule("Editor Group", n => n.Data.ControlType == "List" && n.Parent != null && n.Parent.Data.LandmarkType != null &&n.Parent.Data.LandmarkType.StartsWith("Main") &&n.FindNodeInTree(c => c.Data.ControlType == "ListItem") != null,68, "Variable"),
                    new SemanticRule("Editor Group", n =>n.Data.Name == "Find / Replace",65, "Variable"),
                    // new SemanticRule("Editor Group", n => n.Data.LandmarkType != null && n.Data.LandmarkType.StartsWith("Main") && n.Data.ControlType != "ToolBar" &&n.Data.ControlType != "Tab", 70, "Variable"),
                    new SemanticRule("Panel", n => n.Data.Name == "Panel" && n.Data.ControlType == "Pane", 60, "Variable"),
                    new SemanticRule("Notifications", n => n.Data.ControlType == "List" && n.Data.Name != null && n.Data.Name.Contains("notification"), 40, "Variable")
                });
                return rules.OrderByDescending(r => r.Priority).ToList();
            }
        }

    public class DefaultRuleProvider : ISemanticRuleProvider
    {
        public List<SemanticRule> GetRules(TreeNode windowNode)
        {
            // In DefaultRuleProvider.GetRules method
            return new List<SemanticRule> { new SemanticRule("Title Bar", node => node.Data.ControlType == "TitleBar", 100, "Static") };
        }
    }

        public class SemanticRuleFactory
        {
            public ISemanticRuleProvider GetProvider(AutomationElement windowElement)
            {
                string processName = "unknown";
                try
                {
                    if (windowElement != null && windowElement.Properties.ProcessId.IsSupported)
                    {
                        var process = Process.GetProcessById(windowElement.Properties.ProcessId.Value);
                        processName = process.ProcessName.ToLowerInvariant();
                    }
                }
                catch { /* Ignore errors */ }

                switch (processName)
                {
                    case "explorer": return new FileExplorerRuleProvider();
                    case "code": return new VSCodeRuleProvider();
                    default: return new DefaultRuleProvider();
                }
            }
        }
        // ===================================================================
        // Reaction Rules
        // ===================================================================
        public interface IReactionRuleProvider
        {
            Dictionary<string, ReactionRule> GetReactionMap();
        }

        public class FileExplorerReactionProvider : IReactionRuleProvider
        {
            public Dictionary<string, ReactionRule> GetReactionMap()
            {
                return new Dictionary<string, ReactionRule>
                {
                    { "Title Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Window" }) },
                    { "Address Bar (Root Item)", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","Navigation Pane","NavigationLandmark Landmark"}) },
                    { "Command Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","Temp" }) },
                    { "Navigation Toolbar", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","NavigationLandmark Landmark" }) },
                    { "Status Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "None" }) },
                    { "Details Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Temp" }) },
                    { "Tab Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Window" }) },
                    { "Address Bar (Input)", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","Navigation Toolbar","Address Bar (Input)","NavigationLandmark Landmark" }) },
                    { "Navigation Pane", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","NavigationLandmark Landmark","Navigation Pane" }) },
                    { "Main Content", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","NavigationLandmark Landmark" }) },
                    { "NavigationLandmark Landmark", new ReactionRule(ReactionType.Refresh, new List<string> { "Main Content","Temp","NavigationLandmark Landmark", }) }

                };
            }
        }

        public class VSCodeReactionProvider : IReactionRuleProvider

        {
            public Dictionary<string, ReactionRule> GetReactionMap()
            {
                return new Dictionary<string, ReactionRule>
                {
                    { "Window Controls", new ReactionRule(ReactionType.Temporary, new List<string> { "Window"}) },
                    { "Menu Bar", new ReactionRule(ReactionType.Temporary, new List<string>()) },
                    { "Main Toolbar", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Layout Controls", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Status Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Editor Actions", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Account & Settings Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Activity Bar", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Editor Tabs", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Editor Group", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Panel", new ReactionRule(ReactionType.Refresh, new List<string> { "Editor Tabs", "Editor Group" }) },
                    { "Notifications", new ReactionRule(ReactionType.Refresh, new List<string> { "Side Bar" }) }
                };
            }
        }

        // ===================================================================
        // MAIN SCRAPER CLASS
        // ===================================================================

        public class TopWindowScraper
        {
            private class DiscoveredNode
            {
                public required string DbId { get; init; }
                public string? ParentDbId { get; init; }
                public required AutomationElement Element { get; init; }
            }

            private readonly Dictionary<string, AutomationElement> _elementCache = new();
            private readonly AutomationBase _automation;
            private int _nextId = 0;
            private readonly SemanticRuleFactory _ruleFactory = new();

            public TopWindowScraper(AutomationBase automation)
            {
                _automation = automation;
            }

            public List<SemanticViewComponent> AnalyzeSemantically()
            {
                   // 1. Call the new safe method instead of the old Scrape().
                var flatList = PerformSafeScrape();

                // 2. Check if the scrape failed validation (returned null) or was empty.
                if (flatList == null || !flatList.Any())
                {
                    Console.WriteLine("Semantic analysis halted because a reliable UI scrape could not be obtained.");
                    return new List<SemanticViewComponent>();
                }
                var windowElement = GetElementById(flatList[0].DbId);
                if (windowElement == null) return new List<SemanticViewComponent>();
                var ruleProvider = _ruleFactory.GetProvider(windowElement);
                var rawTree = BuildTree(flatList);
                return BuildSemanticViewHeuristically(rawTree, ruleProvider);
            }

            private List<SemanticViewComponent> BuildSemanticViewHeuristically(List<TreeNode> rawTree, ISemanticRuleProvider ruleProvider)
            {
                if (rawTree.Count == 0) return new List<SemanticViewComponent>();
                var windowNode = rawTree[0];
                var rules = ruleProvider.GetRules(windowNode);
                var classifiedRootsMap = ClassifyNodes(windowNode, rules);
                var semanticView = BuildComponentsFromClassification(classifiedRootsMap);
                var allClaimedSignatures = new HashSet<string>();
                foreach (var component in semanticView)
                {
                    foreach (var rootNode in component.RootNodes)
                    {
                        var nodesInComponent = rootNode.FindAllNodesInTree(_ => true);
                        foreach (var node in nodesInComponent)
                        {
                            allClaimedSignatures.Add(GetNodeSignature(node));
                        }
                    }
                }
                var unclaimedTopLevelNodes = windowNode.Children.Where(child => !allClaimedSignatures.Contains(GetNodeSignature(child))).ToList();
                var prunedOtherNodes = new List<TreeNode>();
                foreach (var node in unclaimedTopLevelNodes)
                {
                    var prunedNode = PruneClaimedNodes(node, allClaimedSignatures);
                    if (prunedNode != null)
                    {
                        prunedOtherNodes.Add(prunedNode);
                    }
                }
                if (prunedOtherNodes.Any())
                {
                    // The "Other Controls" are considered variable
                    semanticView.Add(new SemanticViewComponent("Other Controls", "Variable", prunedOtherNodes));
                }
                return semanticView;
            }

            public List<DesktopScrapedElementDto>? PerformSafeScrape()
            {
                // 1. BOOKEND START: Get the unique ID (handle) of the foreground window.
                IntPtr handleBefore = NativeMethods.GetForegroundWindow();
                if (handleBefore == IntPtr.Zero)
                {
                    Console.WriteLine("VALIDATION FAILED: No foreground window could be identified before scraping.");
                    return null;
                }

                // 2. PERFORM THE SCRAPE
                List<DesktopScrapedElementDto> scrapedData;
                try
                {
                    // Call the original, internal scrape method.
                    scrapedData = Scrape(); 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VALIDATION FAILED: Scrape was interrupted by an internal error: {ex.Message}");
                    return null;
                }

                // 3. BOOKEND END: Get the foreground window handle again.
                IntPtr handleAfter = NativeMethods.GetForegroundWindow();

                // 4. VALIDATE: Compare the handles.
                if (handleBefore != handleAfter)
                {
                    Console.WriteLine("VALIDATION FAILED: Window focus changed during the scrape. The data is considered unreliable.");
                    return null;
                }
                
                // Optional sanity check
                if (scrapedData == null || !scrapedData.Any())
                {
                    Console.WriteLine("VALIDATION FAILED: Scrape completed but returned no elements.");
                    return null;
                }

                // If all checks pass, return the trusted data.
                return scrapedData;
            }


            public List<DesktopScrapedElementDto> Scrape()
            {
                _elementCache.Clear();
                _nextId = 0;
                var foregroundElement = FindForegroundElement();
                if (foregroundElement == null) return new List<DesktopScrapedElementDto>();
                var discoveredNodes = new List<DiscoveredNode>();
                DiscoverElementsRecursive(foregroundElement, null, discoveredNodes);
                var intermediateResults = new List<DesktopScrapedElementDto>();
                foreach (var node in discoveredNodes)
                {
                    if (node.Element.IsAvailable)
                    {
                        string? landmarkType = null;
                        try { landmarkType = node.Element.Properties.LandmarkType.ValueOrDefault.ToString(); }
                        catch (NotSupportedException) { landmarkType = null; }
                        _elementCache[node.DbId] = node.Element;
                        intermediateResults.Add(new DesktopScrapedElementDto(
                            DbId: node.DbId, ParentDbId: node.ParentDbId, Name: node.Element.Properties.Name.ValueOrDefault,
                            ControlType: node.Element.Properties.ControlType.ValueOrDefault.ToString(),
                            ClassName: node.Element.Properties.ClassName.ValueOrDefault, LandmarkType: landmarkType, ParentName: null,
                            BoundingRectangle: node.Element.Properties.BoundingRectangle.ValueOrDefault 
                        ));
                    }
                }
                var filteredResults = intermediateResults.Where(dto => String.IsNullOrEmpty(dto.Name) || !dto.Name.Contains('\u200c')).ToList();
                return PopulateParentNames(filteredResults);
            }

            private List<DesktopScrapedElementDto> PopulateParentNames(List<DesktopScrapedElementDto> elements)
            {
                var finalResults = new List<DesktopScrapedElementDto>();
                var elementMap = elements.ToDictionary(e => e.DbId);
                foreach (var element in elements)
                {
                    string? parentName = null;
                    if (element.ParentDbId != null && elementMap.TryGetValue(element.ParentDbId, out var parentElement))
                    {
                        parentName = GetMeaningfulName(parentElement);
                    }
                    finalResults.Add(element with { ParentName = parentName });
                }
                return finalResults;
            }
            


            public void PrintSemanticView(List<SemanticViewComponent> semanticView)
            {
                Console.WriteLine("\n--- Semantic Window Summary ---");

                var staticComponents = semanticView.Where(c => c.ComponentType == "Static").ToList();
                var variableComponents = semanticView.Where(c => c.ComponentType == "Variable").ToList();

                // Print Static Components
                if (staticComponents.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n\n--- Static Components ---");
                    Console.ResetColor();
                    foreach (var component in staticComponents)
                    {
                        // MERGED: Pass the element cache to the details method.
                        PrintComponentDetails(component, _elementCache);
                    }
                }

                // Print Variable Components
                if (variableComponents.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n\n--- Variable Components ---");
                    Console.ResetColor();
                    foreach (var component in variableComponents)
                    {
                        // MERGED: Pass the element cache to the details method.
                        PrintComponentDetails(component, _elementCache);
                    }
                }
            }


        private void PrintComponentDetails(SemanticViewComponent component, Dictionary<string, AutomationElement> elementCache)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n## {component.ComponentName}");
            Console.ResetColor();
            Console.WriteLine("------------------------------------");
            var discoveredPaths = new List<string>();
            foreach (var rootNode in component.RootNodes)
            {
                // This call remains unchanged, using the original recursive logic.
                GenerateIndentedSummaryRecursive(rootNode, component.ComponentName, discoveredPaths, 0);
            }

            if (discoveredPaths.Any())
            {
                foreach (var path in discoveredPaths)
                {
                    Console.WriteLine(path);
                }
            }
            else
            {
                // MERGED: Added the check from version 2 to not print this message for the editor.
                if (component.ComponentName != "Editor Group")
                {
                    Console.WriteLine("(No important elements found in this component)");
                }
            }

            // MERGED: Added the entire code-printing block from version 2 here.
            // This logic runs only for the "Editor Group" component, after its other elements (if any) are printed.
            if (component.ComponentName == "Editor Group")
            {
                // 1. Find ALL potential editor nodes with a broad filter.
                var potentialEditorNodes = component.RootNodes
                    .SelectMany(root => root.FindAllNodesInTree(n =>
                        n.Data.ControlType == "Edit" &&
                        !string.IsNullOrEmpty(n.Data.DbId)
                    ));

                bool editorFoundAndPrinted = false;

                // 2. Loop through the potential nodes to find the right one.
                foreach (var editorNode in potentialEditorNodes)
                {
                    // 3. Check if the element supports the TextPattern.
                    if (elementCache.TryGetValue(editorNode.Data.DbId, out var editorElement) &&
                        editorElement.Patterns.Text.IsSupported)
                    {
                        // 4. We found the correct element! Print its content.
                        var textPattern = editorElement.Patterns.Text.Pattern;
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("--- Contained Code (via TextPattern) ---");
                        Console.ResetColor();
                        Console.WriteLine(textPattern.DocumentRange.GetText(-1).Trim());

                        editorFoundAndPrinted = true;
                        break; // Stop searching since we found and printed the code.
                    }
                }

                if (!editorFoundAndPrinted)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n--- Code Extraction Failed ---");
                    Console.WriteLine("Could not find an editor pane that supports TextPattern. VS Code accessibility support might be off (set \"editor.accessibilitySupport\": \"on\").");
                    Console.ResetColor();
                }
            }
        }



        // private void PrintComponentDetails(SemanticViewComponent component)
        // {
        //     Console.ForegroundColor = ConsoleColor.Green;
        //     Console.WriteLine($"\n## {component.ComponentName}");
        //     Console.ResetColor();
        //     Console.WriteLine("------------------------------------");
        //     var discoveredPaths = new List<string>();
        //     foreach (var rootNode in component.RootNodes)
        //     {
        //         GenerateIndentedSummaryRecursive(rootNode, component.ComponentName, discoveredPaths, 0);
        //     }

        //     if (discoveredPaths.Any())
        //     {
        //         foreach (var path in discoveredPaths)
        //         {
        //             Console.WriteLine(path);
        //         }
        //     }
        //     else
        //     {
        //         Console.WriteLine("(No important elements found in this component)");
        //     }
        // }


            private string SummarizeNodeDescription(TreeNode node)
            {
                // This refined version correctly ignores interactive children like Buttons.
                var textFragments = node.FindAllNodesInTree(n =>
                        !string.IsNullOrEmpty(n.Data.Name) &&
                        (n.Data.ControlType == "Text" || n == node))
                    .Select(n => n.Data.Name!.Trim())
                    .ToList();

                if (!textFragments.Any())
                {
                    return GetMeaningfulName(node.Data);
                }

                var meaningfulFragments = textFragments
                    .Distinct()
                    .Where(name => !string.IsNullOrEmpty(name) && name.Any(c => char.IsLetterOrDigit(c) || char.IsPunctuation(c)))
                    .ToList();

                var finalFragments = new List<string>();
                foreach (var fragment in meaningfulFragments.OrderByDescending(f => f.Length))
                {
                    if (!finalFragments.Any(f => f.Contains(fragment)))
                    {
                        finalFragments.Add(fragment);
                    }
                }

                // If no meaningful text fragments are found (e.g., only icons), fall back to the simple name.
                if (!finalFragments.Any())
                {
                    return GetMeaningfulName(node.Data);
                }

                return string.Join(" ", finalFragments.OrderBy(f => textFragments.IndexOf(f)));
            }

            private void GenerateIndentedSummaryRecursive(TreeNode node, string name, List<string> discoveredPaths, int depth)
            {
                // This check for trivial wrappers is still correct and useful.
                if ((node.Data.ControlType == "Group" || node.Data.ControlType == "Pane") && node.Children.Count == 1)
                {
                    GenerateIndentedSummaryRecursive(node.Children[0], name, discoveredPaths, depth);
                    return;
                }

                var summarizableContainerTypes = new HashSet<string> { "TreeItem", "ListItem", "TabItem", "SplitButton" };
                if (summarizableContainerTypes.Contains(node.Data.ControlType))
                {
                    string indent = new string(' ', depth * 2);
                    string summarizedName = SummarizeNodeDescription(node);
                    string formattedPath = $"{indent}{name},{node.Data.DbId}:{summarizedName} ({node.Data.ControlType})";
                    discoveredPaths.Add(formattedPath);

                    foreach (var child in node.Children)
                    {
                        if (child.Data.ControlType == "Text") continue;
                        if (child.Data.ControlType == "Group" && !child.Children.Any(c => c.Data.ControlType != "Text")) continue;
                        GenerateIndentedSummaryRecursive(child, name, discoveredPaths, depth + 1);
                    }
                    return;
                }

                var primaryActionTypes = new HashSet<string> { "Button", "CheckBox", "RadioButton", "MenuItem", "Hyperlink" };
                if (primaryActionTypes.Contains(node.Data.ControlType))
                {
                    string indent = new string(' ', depth * 2);
                    string summarizedName = GetMeaningfulName(node.Data); 
                    string formattedPath = $"{indent}{name},{node.Data.DbId}:{summarizedName} ({node.Data.ControlType})";
                    discoveredPaths.Add(formattedPath);
                    return;
                }

                if (IsImportantNode(node) && node.Data.ControlType != "Text")
                {
                    string indent = new string(' ', depth * 2);
                    string meaningfulName = GetMeaningfulName(node.Data);
                    string formattedPath = $"{indent}{name},{node.Data.DbId}:{meaningfulName} ({node.Data.ControlType})";
                    discoveredPaths.Add(formattedPath);
                    foreach (var child in node.Children)
                    {
                        GenerateIndentedSummaryRecursive(child, name, discoveredPaths, depth + 1);
                    }
                }
                else
                {
                    foreach (var child in node.Children)
                    {
                        GenerateIndentedSummaryRecursive(child, name, discoveredPaths, depth);
                    }
                }
            }

        
        private bool IsImportantNode(TreeNode node)
        {
            var data = node.Data;
            var importantTypes = new HashSet<string> { "Button", "CheckBox", "ComboBox", "Hyperlink", "ListItem", "MenuItem", "RadioButton", "TabItem", "TreeItem", "Edit", "Text", "Tree", "List", "ToolBar", "MenuBar", "StatusBar" };
            if (importantTypes.Contains(data.ControlType)) return true;
            bool hasMeaningfulName = !string.IsNullOrEmpty(data.Name) && data.Name != data.ClassName && data.Name != data.ControlType;
            if (hasMeaningfulName) return true;
            return false;
        }

        private string GetNodeSignature(TreeNode node)
        {
            var dto = node.Data;
            return $"{dto.ControlType}|{dto.Name}|{dto.ClassName}";
        }

            private TreeNode? PruneClaimedNodes(TreeNode originalNode, HashSet<string> claimedSignatures)
            {
                if (claimedSignatures.Contains(GetNodeSignature(originalNode))) return null;
                var newNode = new TreeNode(originalNode.Data);
                foreach (var child in originalNode.Children)
                {
                    var prunedChild = PruneClaimedNodes(child, claimedSignatures);
                    if (prunedChild != null)
                    {
                        newNode.Children.Add(prunedChild);
                    }
                }
                if (!newNode.Children.Any() && originalNode.Children.Any()) return null;
                return newNode;
            }
            
            private Dictionary<TreeNode, SemanticRule> ClassifyNodes(TreeNode root, List<SemanticRule> rules)
            {
                var classifiedRoots = new Dictionary<TreeNode, SemanticRule>();
                var claimedNodes = new HashSet<TreeNode>();
                var queue = new Queue<TreeNode>();
                queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    var currentNode = queue.Dequeue();
                    if (claimedNodes.Contains(currentNode)) continue;
                    var matchingRule = rules.FirstOrDefault(rule => rule.Predicate(currentNode));
                    if (matchingRule != null)
                    {
                        classifiedRoots[currentNode] = matchingRule;
                        foreach (var nodeToClaim in currentNode.FindAllNodesInTree(_ => true))
                        {
                            claimedNodes.Add(nodeToClaim);
                        }
                    }
                    else
                    {
                        foreach (var child in currentNode.Children)
                        {
                            queue.Enqueue(child);
                        }
                    }
                }
                return classifiedRoots;
            }
            
            private List<SemanticViewComponent> BuildComponentsFromClassification(Dictionary<TreeNode, SemanticRule> classifiedRoots)
            {
                return classifiedRoots
                    .GroupBy(kvp => kvp.Value.ComponentName) // <--- GROUP BY THE NAME STRING
                    .Select(group => 
                    {
                        // The 'group.Key' is now the string ComponentName (e.g., "Editor Group").
                        // We get the ComponentType from the first rule in the group.
                        var representativeRule = group.First().Value;
                        var componentType = representativeRule.ComponentType;

                        // Collect ALL root nodes from ALL rules in this group.
                        var allRootNodes = group.Select(kvp => kvp.Key).ToList();
                        
                        return new SemanticViewComponent(group.Key, componentType, allRootNodes);
                    })
                    .ToList();
            }

        // private List<SemanticViewComponent> BuildComponentsFromClassification(Dictionary<TreeNode, SemanticRule> classifiedRoots)
        // {
        //     return classifiedRoots
        //         .GroupBy(kvp => kvp.Value)
        //         .Select(group => new SemanticViewComponent(
        //             group.Key.ComponentName,
        //             group.Key.ComponentType,
        //             group.Select(kvp => kvp.Key).ToList()
        //         ))
        //         .ToList();
        // }

        public static List<TreeNode> BuildTree(List<DesktopScrapedElementDto> flatList)
        {
            var treeNodes = new List<TreeNode>();
            var lookup = flatList.ToDictionary(dto => dto.DbId, dto => new TreeNode(dto));
            foreach (var dto in flatList)
            {
                if (dto.ParentDbId != null && lookup.TryGetValue(dto.ParentDbId, out var parentNode))
                {
                    var childNode = lookup[dto.DbId];
                    parentNode.Children.Add(childNode);
                    childNode.Parent = parentNode;
                }
                else
                {
                    treeNodes.Add(lookup[dto.DbId]);
                }
            }
            return treeNodes;
        }

            private static string GetMeaningfulName(DesktopScrapedElementDto element)
            {
                if (!string.IsNullOrEmpty(element.Name) && element.Name != "[Not Supported]") return element.Name;
                if (!string.IsNullOrEmpty(element.ClassName)) return element.ClassName;
                return element.ControlType;
            }

            private void DiscoverElementsRecursive(AutomationElement element, string? parentDbId, List<DiscoveredNode> discoveredNodes)
            {
                string currentDbId = $"dbsa{_nextId++}";
                discoveredNodes.Add(new DiscoveredNode { DbId = currentDbId, ParentDbId = parentDbId, Element = element });
                try
                {
                    var walker = _automation.TreeWalkerFactory.GetRawViewWalker();
                    var child = walker.GetFirstChild(element);
                    while (child != null)
                    {
                        DiscoverElementsRecursive(child, currentDbId, discoveredNodes);
                        child = walker.GetNextSibling(child);
                    }
                }
                catch { /* Ignore errors */ }
            }

            private AutomationElement? FindForegroundElement()
            {
                IntPtr foregroundWindowHandle = NativeMethods.GetForegroundWindow();
                if (foregroundWindowHandle == IntPtr.Zero) return null;
                try { return _automation.FromHandle(foregroundWindowHandle); }
                catch { return null; }
            }

            public AutomationElement? GetElementById(string tempId)
            {
                if (_elementCache.TryGetValue(tempId, out var element) && element.IsAvailable)
                {
                    return element;
                }
                return null;
            }

            // Original, simple click method.
            public bool ExecuteClick(string dbId, string? expectedName)
            {
                var element = GetElementById(dbId);
                if (element == null) return false;

                try
                {
                    element.Focus();
                    element.Click(moveMouse: true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }


    }