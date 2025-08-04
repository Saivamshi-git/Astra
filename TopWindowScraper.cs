using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;

// The DTO record remains the same
public record DesktopScrapedElementDto(
    string DbId,
    string? Name,
    string? ParentDbId,
    string ControlType,
    string? ClassName,
    string? LandmarkType,
    string? ParentName
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

        public TreeNode(DesktopScrapedElementDto data)
        {
            Data = data;
            Children = new List<TreeNode>();
        }
    }

    public class SemanticViewComponent
    {
        public string ComponentName { get; set; }
        public List<TreeNode> RootNodes { get; set; }

        public SemanticViewComponent(string name, List<TreeNode> nodes)
        {
            ComponentName = name;
            RootNodes = nodes;
        }
    }

    public record SemanticRule(string ComponentName, Func<TreeNode, bool> Predicate, int Priority);

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
                new SemanticRule("Title Bar", node => node.Data.ControlType == "TitleBar", 100),
                new SemanticRule("Tab Bar", node => node.Data.ClassName == "Microsoft.UI.Xaml.Controls.TabView", 95),
                new SemanticRule("Address Bar (Root Item)", node => node.Data.ClassName == "FileExplorerExtensions.FirstCrumbStackPanelControl", 91),
                new SemanticRule("Address Bar (Input)", node => node.Data.ClassName == "AutoSuggestBox", 90),
                new SemanticRule("Command Bar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Cut" || n.Data.Name == "View") != null, 85),
                new SemanticRule("Navigation Toolbar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Back") != null, 84),
                new SemanticRule("Details Bar", node => node.Data.ControlType == "AppBar" && node.FindNodeInTree(n => n.Data.Name == "Details") != null, 83),
                new SemanticRule("Status Bar", node => node.Data.ControlType == "StatusBar", 80),
                new SemanticRule("Navigation Pane", node => node.Data.Name == "Navigation Pane" && node.Data.ControlType == "Tree", 50),
                new SemanticRule("Main Content", node => node.Data.Name == "Items View" && node.Data.ControlType == "List", 49),
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
                new SemanticRule("Window Controls", n => n.Data.ControlType == "Button"
                    && (n.Data.Name == "Minimize" || n.Data.Name == "Restore" || n.Data.Name == "Close"),
                    100),

                new SemanticRule("Menu Bar", n => n.Data.ControlType == "MenuBar"
                    && n.FindNodeInTree(c => c.Data.Name == "File") != null, 95),

                new SemanticRule("Main Toolbar", n => n.Data.ControlType == "ToolBar"
                    && n.FindNodeInTree(c => c.Data.Name != null && c.Data.Name.StartsWith("Go Back")) != null, 90),
                
                new SemanticRule("Layout Controls", n => n.Data.Name == "Title actions"
                    && n.Data.ControlType == "ToolBar", 88),

                new SemanticRule("Activity Bar", n => n.Data.Name == "Active View Switcher"
                    && n.Data.ControlType == "Tab", 85),

                new SemanticRule("Side Bar", n => n.Data.ControlType == "Tree" && n.Data.Name == "Files Explorer", 80),

                new SemanticRule("Editor Group", n => n.Data.LandmarkType == "Main", 70),

                new SemanticRule("Editor Actions", n => n.Data.Name == "Editor actions"
                    && n.Data.ControlType == "ToolBar", 69),

                new SemanticRule("Editor Tabs", n => n.Data.ControlType == "Tab"
                    && n.FindNodeInTree(c => c.Data.ControlType == "TabItem") != null
                    && n.FindNodeInTree(c => c.Data.Name == "Active View Switcher") == null,
                    65),

                new SemanticRule("Panel", n => n.Data.Name == "Panel", 60),

                new SemanticRule("Status Bar", n => n.Data.ControlType == "StatusBar", 50),

                new SemanticRule("Notifications", n => n.Data.ControlType == "List"
                    && n.Data.Name != null && n.Data.Name.Contains("notification"), 40)
            });

            return rules.OrderByDescending(r => r.Priority).ToList();
        }
    }

    public class DefaultRuleProvider : ISemanticRuleProvider
    {
        public List<SemanticRule> GetRules(TreeNode windowNode)
        {
            return new List<SemanticRule>
            {
                new SemanticRule("Title Bar", node => node.Data.ControlType == "TitleBar", 100)
            };
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
            catch { /* Ignore errors if process is gone */ }

            switch (processName)
            {
                case "explorer":
                    return new FileExplorerRuleProvider();
                case "code":
                    return new VSCodeRuleProvider();
                default:
                    return new DefaultRuleProvider();
            }
        }
    }

    // ===================================================================
    // INTERACTION ENGINE
    // ===================================================================

    public record AutomationStep(string DbId, string Name, string ControlType, bool IsGateway)
    {
        public static AutomationStep? Parse(string line)
        {
            try
            {
                var parts = line.Trim().Split(new[] { ':' }, 2);
                string dbId = parts[0];
                
                var details = parts[1];
                bool isGateway = details.Contains("[EXPANDS]");
                details = details.Replace("[EXPANDS]", "").Trim();

                var lastParen = details.LastIndexOf('(');
                string name = details.Substring(0, lastParen).Trim();
                string controlType = details.Substring(lastParen + 1, details.Length - lastParen - 2);

                return new AutomationStep(dbId, name, controlType, isGateway);
            }
            catch
            {
                return null;
            }
        }
    }
    
    public class InteractionEngine
    {
        private readonly TopWindowScraper _scraper;
        private readonly AutomationBase _automation;

        public InteractionEngine(TopWindowScraper scraper, AutomationBase automation)
        {
            _scraper = scraper;
            _automation = automation;
        }

        public bool Execute(List<AutomationStep> sequence)
        {
            var currentParent = _scraper.GetElementById("dbsa0"); 
            if (currentParent == null)
            {
                Console.WriteLine("Could not find the main application window. It may be closed.");
                return false;
            }

            foreach (var step in sequence)
            {
                var targetElement = FindElementByProperties(currentParent, step.Name, step.ControlType);

                if (targetElement == null)
                {
                    Console.WriteLine($"Error: Could not find element '{step.Name}' ({step.ControlType}).");
                    return false;
                }

                targetElement.Click(true);
                Console.WriteLine($"Clicked: '{step.Name}'");

                if (step.IsGateway)
                {
                    System.Threading.Thread.Sleep(500);
                    currentParent = targetElement;
                }
            }

            Console.WriteLine("Sequence executed successfully.");
            return true;
        }
        private AutomationElement? FindElementByProperties(AutomationElement parent, string name, string controlType)
        {
            // We use FlaUI's TreeWalker and conditions to perform a robust search.
            var walker = _automation.TreeWalkerFactory.GetControlViewWalker();
            var node = walker.GetFirstChild(parent);

            while (node != null)
            {
                // Check if the current node matches the properties of our step.
                if (node.Name == name && node.ControlType.ToString() == controlType)
                {
                    return node;
                }
                // You could add a recursive search here for deeper matching if needed.
                node = walker.GetNextSibling(node);
            }

            return null;
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
            var flatList = Scrape();
            if (!flatList.Any()) return new List<SemanticViewComponent>();

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
                semanticView.Add(new SemanticViewComponent("Other Controls", prunedOtherNodes));
            }
            return semanticView;
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
                        ClassName: node.Element.Properties.ClassName.ValueOrDefault, LandmarkType: landmarkType, ParentName: null
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

        public enum ElementRole { TerminalAction, GatewayAction, InformationalText, StructuralNoise }

        // Replace your existing GetElementRole method with this one
        private ElementRole GetElementRole(TreeNode node)
        {
            var data = node.Data;
            
            // NEW: First, check for named containers and treat them as important gateways.
            if ((data.ControlType == "Group" || data.ControlType == "Pane") && !string.IsNullOrEmpty(data.Name))
            {
                return ElementRole.GatewayAction;
            }
            
            // Gateways are elements that can be expanded to show children.
            // ADDED: ListItem to this check.
            if ((data.ControlType == "MenuItem" || data.ControlType == "TreeItem" || data.ControlType == "TabItem" || data.ControlType == "ListItem") && node.Children.Any())
            {
                return ElementRole.GatewayAction;
            }

            // Terminal actions are final, interactive elements.
            var terminalActionTypes = new HashSet<string> { "Button", "CheckBox", "ComboBox", "Hyperlink", "RadioButton", "Edit" };
            if (terminalActionTypes.Contains(data.ControlType))
            {
                return ElementRole.TerminalAction;
            }
            
            // Gateways with no children (like a file in a tree) are also terminal actions.
            // ADDED: ListItem to this check.
            if ((data.ControlType == "MenuItem" || data.ControlType == "TreeItem" || data.ControlType == "TabItem" || data.ControlType == "ListItem") && !node.Children.Any())
            {
                return ElementRole.TerminalAction;
            }

            // Purely informational text.
            if (data.ControlType == "Text")
            {
                return ElementRole.InformationalText;
            }

            // Top-level List/Tree containers are gateways to their items.
            if (data.ControlType == "List" || data.ControlType == "Tree")
            {
                return ElementRole.GatewayAction;
            }

            // Everything else is considered structural noise.
            return ElementRole.StructuralNoise;
        }

        // Replace your existing GenerateInteractionMapRecursive method with this one
        private void GenerateInteractionMapRecursive(TreeNode node, List<string> discoveredPaths, int depth)
        {
            ElementRole role = GetElementRole(node);

            // CORRECTED: We only skip pure structural noise. Everything else should be processed.
            if (role == ElementRole.StructuralNoise)
            {
                // Still check children, but don't increment the indent.
                foreach (var child in node.Children)
                {
                    GenerateInteractionMapRecursive(child, discoveredPaths, depth);
                }
                return;
            }

            // This is an important element (Terminal, Gateway, or Informational), so we will print it.
            string indent = new string(' ', depth * 2);
            string meaningfulName = GetMeaningfulName(node.Data);
            
            string marker = (role == ElementRole.GatewayAction) ? " [EXPANDS]" : "";
            
            string formattedLine = $"{indent}{node.Data.DbId}:{meaningfulName} ({node.Data.ControlType}){marker}";
            discoveredPaths.Add(formattedLine);

            // Always recurse into children to find the next steps in the interaction chain.
            foreach (var child in node.Children)
            {
                GenerateInteractionMapRecursive(child, discoveredPaths, depth + 1);
            }
        }

        public void PrintSemanticView(List<SemanticViewComponent> semanticView)
        {
            Console.WriteLine("\n--- Semantic Window Summary ---");
            foreach (var component in semanticView)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n## {component.ComponentName}");
                Console.ResetColor();
                Console.WriteLine("------------------------------------");
                var discoveredPaths = new List<string>();
                foreach (var rootNode in component.RootNodes)
                {
                    GenerateInteractionMapRecursive(rootNode, discoveredPaths, 0);
                }
                foreach (var path in discoveredPaths)
                {
                    Console.WriteLine(path);
                }
            }
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
                .GroupBy(kvp => kvp.Value)
                .Select(group => new SemanticViewComponent(
                    group.Key.ComponentName,
                    group.Select(kvp => kvp.Key).ToList()
                ))
                .ToList();
        }

        public static List<TreeNode> BuildTree(List<DesktopScrapedElementDto> flatList)
        {
            var treeNodes = new List<TreeNode>();
            var lookup = flatList.ToDictionary(dto => dto.DbId, dto => new TreeNode(dto));
            foreach (var dto in flatList)
            {
                if (dto.ParentDbId != null && lookup.TryGetValue(dto.ParentDbId, out var parentNode))
                {
                    parentNode.Children.Add(lookup[dto.DbId]);
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
    }


}