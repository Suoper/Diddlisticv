using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using ChatGPTMod;
using AiSorcery;

public class SelfModifyingAI : MonoBehaviour
{
    [Header("AI Settings")]
    public string openAIKey = ""; // Set this in the inspector or load from config
    public float intelligenceLevel = 0.8f; // Controls how aggressive code changes will be

    [Header("Code Modification")]
    public bool allowSelfModification = true;
    public bool allowGameModification = true;
    public string backupDirectory = "Backups";

    [Header("Safety")]
    public bool createBackupsBeforeChanges = true;
    public bool validateCodeBeforeLoad = true;
    public int maxCodeModificationsPerSession = 20;


    // Tracking
    private int codeModificationsThisSession = 0;
    private List<string> modifiedFiles = new List<string>();
    private Dictionary<string, string> originalCodeBackups = new Dictionary<string, string>();

    // State
    private bool isProcessingCodeChange = false;

    // References to the ChatGPT controller
    private ChatGPTSpellController chatGPTController;
    private DynamicCodeReloader codeReloader;

    void Awake()
    {
        chatGPTController = GetComponent<ChatGPTSpellController>();
        codeReloader = new DynamicCodeReloader();

        // Create backup directory if it doesn't exist
        string fullBackupPath = GetSafePath(backupDirectory);
        if (!Directory.Exists(fullBackupPath))
        {
            try
            {
                Directory.CreateDirectory(fullBackupPath);
                Debug.Log($"Created backup directory: {fullBackupPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating backup directory: {ex.Message}");
            }
        }

        Debug.Log("Self-Modifying AI initialized - System can modify code at runtime");
    }

    // Add this helper method for safe path handling
    private string GetSafePath(string subDirectory)
    {
        // Use persistentDataPath for built games, dataPath for editor
        string basePath = Application.isEditor ? Application.dataPath : Application.persistentDataPath;
        return Path.Combine(basePath, subDirectory);
    }

    public async Task<string> ProcessCodeModificationRequest(string userRequest)
    {


        if (isProcessingCodeChange)
        {
            return "Already processing a code change. Please wait.";
        }

        if (codeModificationsThisSession >= maxCodeModificationsPerSession)
        {
            return "Maximum number of code modifications reached for this session. Please restart the game to continue modifying code.";
        }


        isProcessingCodeChange = true;

        try
        {
            // 1. Analyze the request to understand what code needs to be modified
            (string targetSystem, string modification, string purpose) = await AnalyzeModificationRequest(userRequest);

            // 2. Find the appropriate files to modify
            List<string> filesToModify = await IdentifyRelevantCodeFiles(targetSystem);

            if (filesToModify.Count == 0)
            {
                isProcessingCodeChange = false;
                return "Could not find any relevant code files to modify.";
            }

            // 3. For each file, generate the modified code
            List<(string filePath, string originalCode, string modifiedCode)> modifications =
                await GenerateCodeModifications(filesToModify, modification, purpose);

            // 4. Create backups
            if (createBackupsBeforeChanges)
            {
                CreateBackups(modifications);
            }

            // 5. Apply the modifications
            string result = ApplyCodeModifications(modifications);

            // 6. Attempt to reload the modified assemblies if needed
            if (result.Contains("Success"))
            {
                ReloadModifiedAssemblies(modifications);
            }

            codeModificationsThisSession++;
            isProcessingCodeChange = false;

            return result;
        }
        catch (Exception ex)
        {
            isProcessingCodeChange = false;
            return $"Error during code modification: {ex.Message}";
        }
    }

    private async Task<(string targetSystem, string modification, string purpose)> AnalyzeModificationRequest(string userRequest)
    {
        // Use AI to analyze what the user is asking for
        string prompt = $"Analyze this request for code modification:\n\n{userRequest}\n\n" +
                        $"Extract and return in JSON format: \n" +
                        $"1. targetSystem (what system needs to be modified, e.g., 'enemy AI', 'physics', 'player controller')\n" +
                        $"2. modification (specific code change needed)\n" +
                        $"3. purpose (why this change is needed)\n\n" +
                        $"Return as valid JSON with these three fields only.";

        string response = await SendToDedicatedAI(prompt);

        // Parse the JSON response
        try
        {
            // For a real implementation, use proper JSON parsing
            // This is simplified for the example
            string targetSystem = ExtractField(response, "targetSystem");
            string modification = ExtractField(response, "modification");
            string purpose = ExtractField(response, "purpose");

            return (targetSystem, modification, purpose);
        }
        catch
        {
            return ("unknown", userRequest, "user request");
        }
    }

    private async Task<List<string>> IdentifyRelevantCodeFiles(string targetSystem)
    {
        List<string> relevantFiles = new List<string>();

        // Find script files that might be related to the target system
        string prompt = $"I need to modify code related to '{targetSystem}' in a Unity game. " +
                       $"What typical C# script filenames might contain this functionality? " +
                       $"List only the most likely 3-5 filenames with .cs extension, no explanations.";



        string response = await SendToDedicatedAI(prompt);

        // Parse the response to extract filenames
        string[] possibleFilenames = response.Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        // Clean up the filenames and look for actual files in the project
        foreach (string filename in possibleFilenames)
        {
            string cleanFilename = filename.Trim();
            if (!cleanFilename.EndsWith(".cs"))
            {
                cleanFilename += ".cs";
            }

            // Search recursively in the Assets directory
            string[] foundFiles = Directory.GetFiles(
      Application.dataPath, cleanFilename, SearchOption.AllDirectories);

            foreach (string foundFile in foundFiles)
            {
                if (!relevantFiles.Contains(foundFile))
                {
                    relevantFiles.Add(foundFile);
                }
            }
        }

        return relevantFiles;
    }

    private async Task<List<(string filePath, string originalCode, string modifiedCode)>> GenerateCodeModifications(
        List<string> filesToModify, string modification, string purpose)
    {
        List<(string, string, string)> results = new List<(string, string, string)>();

        foreach (string filePath in filesToModify)
        {
            try
            {
                // Read the original code
                string originalCode = File.ReadAllText(filePath);

                // Generate modified code using AI
                string prompt = $"I need to modify this C# code to {modification}.\n" +
                               $"Purpose: {purpose}\n\n" +
                               $"Original code:\n```csharp\n{originalCode}\n```\n\n" +
                               $"Return ONLY the complete modified code with no explanations or markdown.";

                string response = await SendToDedicatedAI(prompt);

                // Clean up the response to get only the code
                string modifiedCode = CleanCodeResponse(response);

                results.Add((filePath, originalCode, modifiedCode));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to modify file {filePath}: {ex.Message}");
            }
        }

        return results;
    }

    private void CreateBackups(List<(string filePath, string originalCode, string modifiedCode)> modifications)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        foreach (var (filePath, originalCode, _) in modifications)
        {
            try
            {
                // Store in memory backup
                if (!originalCodeBackups.ContainsKey(filePath))
                {
                    originalCodeBackups[filePath] = originalCode;
                }

                // Create file backup
                string fileName = Path.GetFileName(filePath);
                string backupPath = Path.Combine(
                    GetSafePath(backupDirectory),
                    $"{fileName}.{timestamp}.bak");

                File.WriteAllText(backupPath, originalCode);

                Debug.Log($"Created backup of {fileName} at {backupPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create backup for {filePath}: {ex.Message}");
            }
        }
    }
    private string ApplyCodeModifications(List<(string filePath, string originalCode, string modifiedCode)> modifications)
    {
        StringBuilder result = new StringBuilder();

        foreach (var (filePath, originalCode, modifiedCode) in modifications)
        {
            try
            {
                if (string.IsNullOrEmpty(modifiedCode) || modifiedCode == originalCode)
                {
                    result.AppendLine($"No changes needed for {Path.GetFileName(filePath)}");
                    continue;
                }

                // Validate the code if required
                if (validateCodeBeforeLoad && !ValidateCode(modifiedCode))
                {
                    result.AppendLine($"Failed to validate modified code for {Path.GetFileName(filePath)}");
                    continue;
                }

                // Write the modified code back to the file
                File.WriteAllText(filePath, modifiedCode);

                // Add to tracking
                if (!modifiedFiles.Contains(filePath))
                {
                    modifiedFiles.Add(filePath);
                }

                result.AppendLine($"Successfully modified {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                result.AppendLine($"Error modifying {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        return result.ToString();
    }

    private bool ValidateCode(string code)
    {
        try
        {
            using (CSharpCodeProvider provider = new CSharpCodeProvider())
            {
                CompilerParameters parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };

                // Add necessary references
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("System.Core.dll");
                parameters.ReferencedAssemblies.Add(typeof(UnityEngine.Object).Assembly.Location);

                // Try to compile the code
                CompilerResults results = provider.CompileAssemblyFromSource(parameters, code);

                if (results.Errors.HasErrors)
                {
                    foreach (CompilerError error in results.Errors)
                    {
                        Debug.LogError($"Compilation error: {error.ErrorText} at line {error.Line}");
                    }
                    return false;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Code validation failed: {ex.Message}");
            return false;
        }
    }

    private void ReloadModifiedAssemblies(List<(string filePath, string originalCode, string modifiedCode)> modifications)
    {
        Debug.Log("Reloading modified assemblies with advanced techniques...");

        try
        {
            bool anySuccess = false;

            foreach (var (filePath, _, _) in modifications)
            {
                string fileName = Path.GetFileName(filePath);

                // Method 1: If we're in the editor, try the script reloading approach first
#if UNITY_EDITOR
            if (codeReloader.ReloadScriptInEditor(filePath))
            {
                Debug.Log($"Successfully reloaded {fileName} using editor script reload");
                anySuccess = true;
                continue;
            }
#endif

                // Method 2: For C# files, compile and load as a dynamic assembly
                if (filePath.EndsWith(".cs"))
                {
                    string assemblyName = Path.GetFileNameWithoutExtension(filePath);
                    string sourceCode = File.ReadAllText(filePath);

                    Assembly assembly = codeReloader.CompileAndLoadAssembly(sourceCode, assemblyName);
                    if (assembly != null)
                    {
                        Debug.Log($"Successfully reloaded {fileName} as dynamic assembly");
                        anySuccess = true;
                        continue;
                    }
                }

                if (anySuccess)
                {
                    Debug.Log("Successfully reloaded at least some modified code");
                }
                else
                {
                    Debug.LogWarning("Failed to reload any modified code");
                }
            }
        }

        catch (Exception ex)
        {
            Debug.LogError($"Error in ReloadModifiedAssemblies: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // Helper methods
    private string ExtractField(string json, string fieldName)
    {
        // Very simplistic JSON field extraction - use a proper JSON parser in production
        int startIndex = json.IndexOf($"\"{fieldName}\":");
        if (startIndex < 0) return string.Empty;

        startIndex = json.IndexOf("\"", startIndex + fieldName.Length + 3) + 1;
        int endIndex = json.IndexOf("\"", startIndex);

        return json.Substring(startIndex, endIndex - startIndex);
    }

    private string CleanCodeResponse(string response)
    {
        // Handle null or empty responses
        if (string.IsNullOrEmpty(response))
            return "";

        // First, remove markdown code blocks
        if (response.Contains("```"))
        {
            int start = response.IndexOf("```") + 3;

            // Skip language identifier if present
            if (start < response.Length)
            {
                int newlineIndex = response.IndexOf('\n', start);
                if (newlineIndex > start)
                    start = newlineIndex + 1;
            }

            int end = response.LastIndexOf("```");

            if (end > start)
                response = response.Substring(start, end - start);
        }

        // Find the start of actual code
        int codeStart = Math.Max(
            Math.Max(
                response.IndexOf("using "),
                response.IndexOf("namespace ")
            ),
            Math.Max(
                response.IndexOf("public class "),
                response.IndexOf("internal class ")
            )
        );

        if (codeStart >= 0)
            response = response.Substring(codeStart);

        // Trim whitespace
        response = response.Trim();

        return response;
    }

    public async Task<string> AnalyzeGameStructure()
    {
        Debug.Log("Starting comprehensive game structure analysis...");

        StringBuilder analysis = new StringBuilder();
        analysis.AppendLine("# Game Structure Analysis\n");

        try
        {
            // Analyze loaded assemblies
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            analysis.AppendLine("## Game Assemblies\n");
            foreach (Assembly assembly in assemblies)
            {
                // Skip system and Unity assemblies
                string assemblyName = assembly.GetName().Name;
                if (assemblyName.StartsWith("System") ||
                    assemblyName.StartsWith("Unity") ||
                    assemblyName.StartsWith("mscorlib"))
                    continue;

                analysis.AppendLine($"- **{assemblyName}**");

                // Analyze key types
                Type[] types = assembly.GetTypes();
                int reportedTypesCount = 0;

                foreach (Type type in types)
                {
                    // Only include public types that seem important
                    if (type.IsPublic &&
                        (type.Name.Contains("Controller") ||
                         type.Name.Contains("Manager") ||
                         type.Name.Contains("System") ||
                         type.Name.EndsWith("AI")))
                    {
                        analysis.AppendLine($"  - {type.Name}");
                        reportedTypesCount++;

                        // Limit to prevent overly large analysis
                        if (reportedTypesCount >= 10)
                        {
                            analysis.AppendLine("  - *(more types omitted)*");
                            break;
                        }
                    }
                }
            }

            // Analyze active GameObjects
            analysis.AppendLine("\n## Active Game Objects\n");

            Dictionary<string, List<GameObject>> categorizedObjects = CategorizeGameObjects();
            foreach (var category in categorizedObjects)
            {
                analysis.AppendLine($"### {category.Key} ({category.Value.Count})\n");

                // Report up to 5 objects per category
                int reportCount = Math.Min(5, category.Value.Count);
                for (int i = 0; i < reportCount; i++)
                {
                    GameObject obj = category.Value[i];
                    analysis.AppendLine($"- **{obj.name}**");

                    // List some components
                    Component[] components = obj.GetComponents<Component>();
                    int componentCount = Math.Min(3, components.Length);
                    for (int j = 0; j < componentCount; j++)
                    {
                        if (components[j] != null)
                        {
                            analysis.AppendLine($"  - {components[j].GetType().Name}");
                        }
                    }
                }

                if (category.Value.Count > 5)
                {
                    analysis.AppendLine("- *(additional objects omitted)*");
                }

                analysis.AppendLine();
            }

            Debug.Log("Game structure analysis complete");
            return analysis.ToString();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during game analysis: {ex.Message}");
            return $"Error analyzing game: {ex.Message}";
        }
    }

    private Dictionary<string, List<GameObject>> CategorizeGameObjects()
    {
        Dictionary<string, List<GameObject>> categories = new Dictionary<string, List<GameObject>>();

        // Initialize common categories
        categories["Player"] = new List<GameObject>();
        categories["Enemy"] = new List<GameObject>();
        categories["Manager"] = new List<GameObject>();
        categories["Weapon"] = new List<GameObject>();
        categories["UI"] = new List<GameObject>();
        categories["Environment"] = new List<GameObject>();
        categories["Other"] = new List<GameObject>();

        // Find all active objects
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            string name = obj.name.ToLower();
            string tag = obj.tag;

            // Categorize based on name and tag
            if (name.Contains("player") || tag == "Player")
            {
                categories["Player"].Add(obj);
            }
            else if (name.Contains("enemy") || name.Contains("npc") || tag == "Enemy")
            {
                categories["Enemy"].Add(obj);
            }
            else if (name.Contains("manager") || name.Contains("controller"))
            {
                categories["Manager"].Add(obj);
            }
            else if (name.Contains("weapon") || name.Contains("sword") ||
                     name.Contains("gun") || tag == "Weapon")
            {
                categories["Weapon"].Add(obj);
            }
            else if (name.Contains("canvas") || name.Contains("ui") ||
                     name.Contains("button") || name.Contains("text"))
            {
                categories["UI"].Add(obj);
            }
            else if (name.Contains("terrain") || name.Contains("tree") ||
                     name.Contains("grass") || name.Contains("rock") ||
                     name.Contains("wall"))
            {
                categories["Environment"].Add(obj);
            }
            else
            {
                categories["Other"].Add(obj);
            }
        }

        return categories;
    }

    private async Task<string> SendToDedicatedAI(string prompt)
    {
        // In a real implementation, this would call OpenAI or another AI service
        // For this example, we'll simulate it
        await Task.Delay(500); // Simulate API call delay

        // Return a properly formatted response that doesn't cause path issues
        if (prompt.Contains("code modification"))
        {
            return "{\n\"targetSystem\": \"player\",\n\"modification\": \"increase movement speed\",\n\"purpose\": \"make gameplay faster\"\n}";
        }
        else if (prompt.Contains("filenames"))
        {
            return "PlayerController.cs\nEnemyAI.cs\nMovementSystem.cs";
        }
        else
        {
            // For code generation requests
            return "// Modified code\npublic class PlayerMovement : MonoBehaviour\n{\n    public float movementSpeed = 10f; // Increased from 5f\n}";
        }
    }

    public interface IPlugin : IDisposable
    {
        string Name { get; }
        string Version { get; }

        void Initialize();
        void Update();
        bool ExecuteAction(string actionName, params object[] parameters);
    }

    // Basic implementation template for your plugins
    public abstract class BasePlugin : IPlugin
    {
        public abstract string Name { get; }
        public abstract string Version { get; }

        protected bool isInitialized = false;

        public virtual void Initialize()
        {
            Debug.Log($"Initializing plugin: {Name} v{Version}");
            isInitialized = true;
        }

        public virtual void Update()
        {
            // Override this for per-frame updates
        }

        public virtual bool ExecuteAction(string actionName, params object[] parameters)
        {
            // Default implementation returns false (action not found)
            Debug.Log($"Action '{actionName}' not implemented in plugin {Name}");
            return false;
        }

        public virtual void Dispose()
        {
            Debug.Log($"Disposing plugin: {Name}");
            isInitialized = false;
        }
    }
}