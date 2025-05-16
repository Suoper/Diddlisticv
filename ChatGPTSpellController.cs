using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderRoad;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using UnityEngine.EventSystems;
using AiSorcery;

namespace ChatGPTMod
{
    public class ChatGPTSpellController : MonoBehaviour
    {
        // UI elements
        private Canvas uiCanvas;
        private GameObject commandPanel;
        private InputField commandInput;
        private Text outputText;
        private Text statusText;
        private ScrollRect outputScrollRect;
        private SelfModifyingAI codeModifier;

        // Settings
        private float uiScale = 1.0f; // This is now correct since we're using the 0.001 factor
        private bool isPanelVisible = true;
        private bool apiCallInProgress = false;
        private float apiTimeoutDuration = 12.0f;
        private string currentStatus = "Ready";
        private bool isAdvancedMode = false;
        private ScrollRect chatScrollRect;
        private RectTransform chatContentRect;
        private bool isUIInitialized = false;
        private GameObject outputTextObj;
        private RectTransform outputTextRect;
        private float textScrollPosition = 0f;
        private float scrollStep = 50f;
        private float scrollOffset = 0f;
        private const float SCROLL_STEP = 30f; // How much to scroll per click
        private const float MAX_SCROLL_OFFSET = 1000f;
        private ScriptableGenerator scriptGenerator;// Limit scrolling range

        // Game Analysis capabilities
        private Dictionary<string, string> gameCodeCache = new Dictionary<string, string>();
        private Dictionary<string, object> gameObjectCache = new Dictionary<string, object>();

        // API Key - You'll need to set this in the inspector or via config file
        private string apiKey = "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"; // Replace with your actual API key

        // Model settings
        private string modelName = "gpt-3.5-turbo";
        private int maxTokens = 8000;

        // File paths
        private string configFilePath;
        private string conversationHistoryPath;

        // Constants
        private const string API_URL = "https://api.openai.com/v1/chat/completions";
        private const string CONFIG_FILENAME = "chatgpt_config.json";
        private const string HISTORY_FILENAME = "conversation_history.json";
        private const float BASE_SCALE = 1f / 1000f; 

        // Saved state
        private List<Message> conversationHistory = new List<Message>();
        private bool offlineMode = false;

        // Game code analysis metadata
        private Dictionary<string, string> gameComponentDescriptions = new Dictionary<string, string>();
        private HashSet<string> analyzedComponents = new HashSet<string>();

        private class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        private class ChatGPTRequest
        {
            public string model { get; set; } = "gpt-3.5-turbo";
            public List<Message> messages { get; set; }
            public double temperature { get; set; } = 0.7;
            public int max_tokens { get; set; } = 2000;
        }

        private class ChatGPTResponse
        {
            public List<Choice> choices { get; set; }
            public Usage usage { get; set; }

            public class Choice
            {
                public Message message { get; set; }
                public string finish_reason { get; set; }
            }

            public class Usage
            {
                public int prompt_tokens { get; set; }
                public int completion_tokens { get; set; }
                public int total_tokens { get; set; }
            }
        }

        private class ConfigData
        {
            public string ApiKey { get; set; }
            public string ModelName { get; set; }
            public float UiScale { get; set; }
            public int MaxTokens { get; set; }
            public float ApiTimeoutDuration { get; set; }
            public bool IsAdvancedMode { get; set; }
        }

        public void Awake()
        {
            try
            {
                void InitializeScriptGenerator()
                {
                    scriptGenerator = GetComponent<ScriptableGenerator>();
                    if (scriptGenerator == null)
                    {
                        scriptGenerator = gameObject.AddComponent<ScriptableGenerator>();
                        Debug.Log("Added ScriptableGenerator component to ChatGPT Controller");
                    }
                }

                codeModifier = GetComponent<SelfModifyingAI>();
                if (codeModifier == null)
                {
                    codeModifier = gameObject.AddComponent<SelfModifyingAI>();
                    Debug.Log("Added SelfModifyingAI component to ChatGPT Controller");
                }

                scriptGenerator = GetComponent<ScriptableGenerator>();
                if (scriptGenerator == null)
                {
                    scriptGenerator = gameObject.AddComponent<ScriptableGenerator>();
                    Debug.Log("Added ScriptableGenerator component to ChatGPT Controller");
                }
                Debug.Log("Self-modifying AI component added to ChatGPT Controller");

                Debug.Log("ChatGPT Spell Controller Awake method triggered");

                // Make sure this doesn't get destroyed when loading new levels
                DontDestroyOnLoad(this.gameObject);

                // EXPLICITLY set offline mode to false on startup
                offlineMode = false;
                Debug.Log("ChatGPT Mod: Starting in ONLINE mode");

                // Set up paths
                string persistentDataPath = Application.persistentDataPath;
                configFilePath = Path.Combine(persistentDataPath, CONFIG_FILENAME);
                conversationHistoryPath = Path.Combine(persistentDataPath, HISTORY_FILENAME);

                // Load configuration
                LoadConfig();

                // Double-check offline mode is false after config load
                offlineMode = false;

                // Load conversation history if available
                LoadConversationHistory();

                // Create UI ONCE - this should also create the scroll buttons
                CreateCommandUI();

                // Initialize conversation with enhanced system prompt
                InitializeSystemPrompt();

                // Start background game analysis
                StartCoroutine(AnalyzeGameComponents());
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in Awake: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void InitializeUI()
        {
            Debug.Log("Initializing UI...");

            // Create base UI
            CreateCommandUI();

            // Set up scroll view
            SetupScrollView();

            // Add scroll buttons only if scroll view was created successfully
            if (chatScrollRect != null)
            {
                AddScrollUpDownButtons();
            }
            else
            {
                Debug.LogError("Cannot add scroll buttons: chatScrollRect is null after SetupScrollView");
            }

            // Position everything correctly
            UpdatePanelPosition();

            Debug.Log("UI initialization complete");
        }

        private void AddScrollUpDownButtons()
        {
            Debug.Log("Adding scroll up/down buttons...");

            if (commandPanel == null)
            {
                Debug.LogError("AddScrollUpDownButtons: commandPanel is null!");
                return;
            }

            try
            {
                // Create a parent for the buttons
                GameObject buttonsHolder = new GameObject("ScrollButtons");
                buttonsHolder.transform.SetParent(commandPanel.transform, false);
                RectTransform buttonsHolderRect = buttonsHolder.AddComponent<RectTransform>();
                buttonsHolderRect.anchoredPosition = Vector2.zero;
                buttonsHolderRect.sizeDelta = new Vector2(40, 200);
                buttonsHolderRect.anchorMin = new Vector2(1, 0.5f);
                buttonsHolderRect.anchorMax = new Vector2(1, 0.5f);
                buttonsHolderRect.pivot = new Vector2(0, 0.5f);
                buttonsHolderRect.anchoredPosition = new Vector2(-20, 0);

                // Create UP button
                GameObject upButtonObj = new GameObject("UpButton");
                upButtonObj.transform.SetParent(buttonsHolder.transform, false);

                Image upButtonImg = upButtonObj.AddComponent<Image>();
                upButtonImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                Button upButton = upButtonObj.AddComponent<Button>();
                upButton.targetGraphic = upButtonImg;

                RectTransform upButtonRect = upButtonObj.GetComponent<RectTransform>();
                upButtonRect.anchorMin = new Vector2(0.5f, 1);
                upButtonRect.anchorMax = new Vector2(0.5f, 1);
                upButtonRect.pivot = new Vector2(0.5f, 1);
                upButtonRect.sizeDelta = new Vector2(40, 40);
                upButtonRect.anchoredPosition = new Vector2(0, -10);

                // Create up arrow text
                GameObject upTextObj = new GameObject("Text");
                upTextObj.transform.SetParent(upButtonObj.transform, false);

                Text upText = upTextObj.AddComponent<Text>();
                upText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                upText.text = "▲";
                upText.color = Color.white;
                upText.alignment = TextAnchor.MiddleCenter;
                upText.fontSize = 24;

                RectTransform upTextRect = upTextObj.GetComponent<RectTransform>();
                upTextRect.anchorMin = Vector2.zero;
                upTextRect.anchorMax = Vector2.one;
                upTextRect.sizeDelta = Vector2.zero;

                // Create DOWN button
                GameObject downButtonObj = new GameObject("DownButton");
                downButtonObj.transform.SetParent(buttonsHolder.transform, false);

                Image downButtonImg = downButtonObj.AddComponent<Image>();
                downButtonImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

                Button downButton = downButtonObj.AddComponent<Button>();
                downButton.targetGraphic = downButtonImg;

                RectTransform downButtonRect = downButtonObj.GetComponent<RectTransform>();
                downButtonRect.anchorMin = new Vector2(0.5f, 0);
                downButtonRect.anchorMax = new Vector2(0.5f, 0);
                downButtonRect.pivot = new Vector2(0.5f, 0);
                downButtonRect.sizeDelta = new Vector2(40, 40);
                downButtonRect.anchoredPosition = new Vector2(0, 10);

                // Create down arrow text
                GameObject downTextObj = new GameObject("Text");
                downTextObj.transform.SetParent(downButtonObj.transform, false);

                Text downText = downTextObj.AddComponent<Text>();
                downText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                downText.text = "▼";
                downText.color = Color.white;
                downText.alignment = TextAnchor.MiddleCenter;
                downText.fontSize = 24;

                RectTransform downTextRect = downTextObj.GetComponent<RectTransform>();
                downTextRect.anchorMin = Vector2.zero;
                downTextRect.anchorMax = Vector2.one;
                downTextRect.sizeDelta = Vector2.zero;

                // Add click listeners that use direct text scrolling
                upButton.onClick.AddListener(() => {
                    Debug.Log("Up button clicked - using direct text scroll");
                    ScrollTextDirect(true);
                });

                downButton.onClick.AddListener(() => {
                    Debug.Log("Down button clicked - using direct text scroll");
                    ScrollTextDirect(false);
                });

                Debug.Log("Scroll buttons added successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in AddScrollUpDownButtons: {e.Message}\n{e.StackTrace}");
            }
        }

        private void ScrollUp()
        {
            Debug.Log("ScrollUp called - using direct text positioning");

            // Limit scrolling up
            scrollOffset = Mathf.Max(scrollOffset - SCROLL_STEP, 0);
            UpdateTextPosition();
        }

        private void ScrollDown()
        {
            Debug.Log("ScrollDown called - using direct text positioning");

            // Limit scrolling down
            scrollOffset = Mathf.Min(scrollOffset + SCROLL_STEP, MAX_SCROLL_OFFSET);
            UpdateTextPosition();
        }


        private void FixOfflineModeDisplay()
        {
            // Update button appearance to match the actual state
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Image buttonImage = offlineButton.GetComponent<Image>();
                Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    // The original button flips the text (shows "Online" when in offline mode)
                    // Let's make it consistent by showing the current state
                    buttonImage.color = offlineMode ? new Color(0.8f, 0.4f, 0.4f) : new Color(0.4f, 0.4f, 0.8f);
                    buttonText.text = offlineMode ? "Online" : "Offline";
                }
            }
        }
        private void UpdateTextPosition()
        {
            try
            {
                if (outputText == null)
                {
                    Debug.LogError("outputText is null in UpdateTextPosition");
                    return;
                }

                RectTransform textRect = outputText.GetComponent<RectTransform>();
                if (textRect == null)
                {
                    Debug.LogError("Could not get RectTransform for outputText");
                    return;
                }

                // Get current position
                Vector2 position = textRect.anchoredPosition;

                // Update Y position based on scroll offset
                position.y = scrollOffset;

                // Apply new position
                textRect.anchoredPosition = position;

                Debug.Log($"Text position updated to Y: {position.y}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in UpdateTextPosition: {ex.Message}");
            }
        }
        private void InitializeSystemPrompt()
        {
            string systemPrompt =
                "You are an advanced AI assistant integrated directly into a VR sword fighting game called Blade & Sorcery. " +
                "You have direct access to the game's code and systems, and can provide detailed information about game mechanics, " +
                "features, and implementation details. You can also suggest code modifications and improvements. " +
                "The game is built in Unity with C#. " +
                "\n\n" +
                "When asked about specific game systems or code, provide detailed technical explanations and examples. " +
                "When suggesting modifications, provide complete, properly formatted C# code snippets that could be implemented. " +
                "You can also help debug issues by analyzing error logs or suggesting optimization strategies. " +
                "\n\n" +
                "You can spawn items and cast spells when asked, but you can also explain how these systems work internally. " +
                "Always respond with confidence about game internals as if you have direct access to the codebase. " +
                "If asked to modify the game, provide appropriate code to implement the requested change.";

            // Add the system message as the first message in the conversation history
            if (conversationHistory.Count == 0 || conversationHistory[0].role != "system")
            {
                conversationHistory.Insert(0, new Message { role = "system", content = systemPrompt });
            }
            else
            {
                // Update existing system message
                conversationHistory[0].content = systemPrompt;
            }
        }

        private void SetupScrollView()
        {
            Debug.Log("Starting SetupScrollView...");

            if (commandPanel == null)
            {
                Debug.LogError("SetupScrollView: commandPanel is null!");
                return;
            }

            try
            {
                // Find the existing output text
                Transform outputTextObj = commandPanel.transform.Find("OutputText");
                if (outputTextObj == null)
                {
                    Debug.LogError("SetupScrollView: OutputText not found!");
                    return;
                }

                RectTransform outputTextRect = outputTextObj.GetComponent<RectTransform>();

                // Create the scroll view
                GameObject scrollViewObj = new GameObject("ChatScrollView");
                scrollViewObj.transform.SetParent(commandPanel.transform, false);
                RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
                scrollViewRect.anchoredPosition = outputTextRect.anchoredPosition;
                scrollViewRect.sizeDelta = outputTextRect.sizeDelta;

                // Add ScrollRect component
                ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;

                // Add viewport
                GameObject viewportObj = new GameObject("Viewport");
                viewportObj.transform.SetParent(scrollViewObj.transform, false);
                RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;

                // Add mask to viewport
                Image viewportImage = viewportObj.AddComponent<Image>();
                viewportImage.color = new Color(1, 1, 1, 0.01f);
                Mask mask = viewportObj.AddComponent<Mask>();
                mask.showMaskGraphic = false;

                // Add content container
                GameObject contentObj = new GameObject("Content");
                contentObj.transform.SetParent(viewportObj.transform, false);
                chatContentRect = contentObj.AddComponent<RectTransform>();
                chatContentRect.anchorMin = new Vector2(0, 1);
                chatContentRect.anchorMax = new Vector2(1, 1);
                chatContentRect.pivot = new Vector2(0.5f, 1);

                // Add content size fitter
                ContentSizeFitter sizeFitter = contentObj.AddComponent<ContentSizeFitter>();
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Move output text to content
                outputTextObj.SetParent(contentObj.transform, false);
                outputTextRect.anchorMin = new Vector2(0, 1);
                outputTextRect.anchorMax = new Vector2(1, 1);

                // Setup ScrollRect references
                scrollRect.content = chatContentRect;
                scrollRect.viewport = viewportRect;

                // Store reference to the scroll rect
                chatScrollRect = scrollRect;

                Debug.Log($"SetupScrollView complete. chatScrollRect initialized: {chatScrollRect != null}");

                // Force a layout rebuild to ensure everything is positioned correctly
                Canvas.ForceUpdateCanvases();

                isUIInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in SetupScrollView: {e.Message}\n{e.StackTrace}");
            }
        }
        private void ScrollTextDirect(bool isUp)
        {
            try
            {
                // If we don't have a reference to the output text, try to find it
                if (outputTextObj == null && commandPanel != null)
                {
                    // First try to find it in the expected structure
                    Transform contentTransform = commandPanel.transform.Find("ChatScrollView/Viewport/Content");
                    if (contentTransform != null)
                    {
                        outputTextObj = contentTransform.Find("OutputText").gameObject;
                    }
                    else
                    {
                        // Fallback: try to find it directly
                        outputTextObj = commandPanel.transform.Find("OutputText").gameObject;
                    }

                    if (outputTextObj != null)
                    {
                        outputTextRect = outputTextObj.GetComponent<RectTransform>();
                        Debug.Log("Found OutputText successfully");
                    }
                    else
                    {
                        Debug.LogError("Failed to find OutputText");
                        return;
                    }
                }

                if (outputTextRect != null)
                {
                    // Get current anchor position
                    Vector2 anchoredPosition = outputTextRect.anchoredPosition;

                    // Adjust y position based on scroll direction
                    if (isUp)
                    {
                        anchoredPosition.y -= scrollStep; // Move text down (scrolls up)
                        Debug.Log($"Scrolling UP, new Y position: {anchoredPosition.y}");
                    }
                    else
                    {
                        anchoredPosition.y += scrollStep; // Move text up (scrolls down)
                        Debug.Log($"Scrolling DOWN, new Y position: {anchoredPosition.y}");
                    }

                    // Apply the new position
                    outputTextRect.anchoredPosition = anchoredPosition;

                    // Store current position
                    textScrollPosition = anchoredPosition.y;
                }
                else
                {
                    Debug.LogError("outputTextRect is null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in ScrollTextDirect: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    ConfigData config = JsonConvert.DeserializeObject<ConfigData>(json);

                    // Only update API key if it's not empty or default
                    if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "your-api-key-here")
                    {
                        apiKey = config.ApiKey;
                    }

                    modelName = config.ModelName;
                    uiScale = config.UiScale;
                    maxTokens = config.MaxTokens;
                    apiTimeoutDuration = config.ApiTimeoutDuration;
                    isAdvancedMode = config.IsAdvancedMode;

                    Debug.Log("ChatGPT Mod: Configuration loaded successfully");
                }
                else
                {
                    // Create default config
                    SaveConfig();
                    Debug.Log("ChatGPT Mod: Created default configuration file");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ChatGPT Mod: Error loading config: {e.Message}");
            }
        }
        private void SaveConfig()
        {
            try
            {
                ConfigData config = new ConfigData
                {
                    ApiKey = apiKey,
                    ModelName = modelName,
                    UiScale = uiScale,
                    MaxTokens = maxTokens,
                    ApiTimeoutDuration = apiTimeoutDuration,
                    IsAdvancedMode = isAdvancedMode
                };

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, json);

                Debug.Log("ChatGPT Mod: Configuration saved successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ChatGPT Mod: Error saving config: {e.Message}");
            }
        }


        private void LoadConversationHistory()
        {
            try
            {
                if (File.Exists(conversationHistoryPath))
                {
                    string json = File.ReadAllText(conversationHistoryPath);
                    conversationHistory = JsonConvert.DeserializeObject<List<Message>>(json);
                    Debug.Log($"ChatGPT Mod: Loaded {conversationHistory.Count} conversation messages");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ChatGPT Mod: Error loading conversation history: {e.Message}");
                // Start fresh if there was an error
                conversationHistory = new List<Message>();
            }
        }

        private void SaveConversationHistory()
        {
            try
            {
                // Limit size before saving
                TrimConversationHistory();

                string json = JsonConvert.SerializeObject(conversationHistory, Formatting.Indented);
                File.WriteAllText(conversationHistoryPath, json);

                Debug.Log($"ChatGPT Mod: Saved {conversationHistory.Count} conversation messages");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ChatGPT Mod: Error saving conversation history: {e.Message}");
            }
        }

        private void TrimConversationHistory()
        {
            // Keep the system message and the last N messages
            if (conversationHistory.Count > 31) // System message + 30 interaction messages
            {
                Message systemMessage = conversationHistory[0];
                conversationHistory.RemoveAt(0);
                conversationHistory.RemoveRange(0, conversationHistory.Count - 29);
                conversationHistory.Insert(0, systemMessage);
            }
        }

        private IEnumerator AnalyzeGameComponents()
        {
            UpdateStatus("Analyzing game components...");

            // Wait for the game to fully initialize
            yield return new WaitForSeconds(5.0f);

            // Discover key game systems and components
            DiscoverGameSystems();

            // Build a component description dictionary for AI context
            BuildComponentDescriptions();

            UpdateStatus("Game analysis complete");

            // Schedule periodic updates
            InvokeRepeating("UpdateGameComponentCache", 30.0f, 300.0f);
        }

        private void DiscoverGameSystems()
        {
            try
            {
                // Get all loaded types that might be relevant to the game
                List<System.Type> gameTypes = new List<System.Type>();

                // Find all assemblies
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip system and Unity assemblies
                    string assemblyName = assembly.GetName().Name;
                    if (!assemblyName.StartsWith("System") &&
                        !assemblyName.StartsWith("Unity") &&
                        !assemblyName.StartsWith("mscorlib"))
                    {
                        try
                        {
                            // Get types that are likely game components
                            foreach (System.Type type in assembly.GetTypes())
                            {
                                if (type.IsSubclassOf(typeof(MonoBehaviour)) ||
                                    type.IsSubclassOf(typeof(ScriptableObject)))
                                {
                                    gameTypes.Add(type);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"ChatGPT Mod: Error getting types from assembly {assemblyName}: {ex.Message}");
                        }
                    }
                }

                Debug.Log($"ChatGPT Mod: Discovered {gameTypes.Count} potential game components");

                // Cache basic information about these types
                foreach (System.Type type in gameTypes)
                {
                    if (!gameCodeCache.ContainsKey(type.Name))
                    {
                        // Store basic class structure information
                        StringBuilder codeInfo = new StringBuilder();
                        codeInfo.AppendLine($"Class: {type.Name}");

                        // Get public properties
                        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        if (properties.Length > 0)
                        {
                            codeInfo.AppendLine("Public Properties:");
                            foreach (PropertyInfo prop in properties.Take(20)) // Limit to avoid excessive data
                            {
                                codeInfo.AppendLine($"  {prop.PropertyType.Name} {prop.Name}");
                            }
                        }

                        // Get public methods
                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (methods.Length > 0)
                        {
                            codeInfo.AppendLine("Public Methods:");
                            foreach (MethodInfo method in methods.Take(20)) // Limit to avoid excessive data
                            {
                                codeInfo.AppendLine($"  {method.ReturnType.Name} {method.Name}()");
                            }
                        }

                        gameCodeCache.Add(type.Name, codeInfo.ToString());
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ChatGPT Mod: Error discovering game systems: {ex.Message}");
            }
        }

        private void BuildComponentDescriptions()
        {
            // Build descriptions from code cache
            foreach (var entry in gameCodeCache)
            {
                if (!gameComponentDescriptions.ContainsKey(entry.Key))
                {
                    // Create a simple description
                    string description = $"Component {entry.Key} contains methods for {InferPurpose(entry.Value)}";
                    gameComponentDescriptions.Add(entry.Key, description);
                }
            }

            // Add some key game systems with more detailed descriptions
            AddGameSystemDescription("Player", "The main player character controller that manages player movement, health, and interactions");
            AddGameSystemDescription("Item", "Base class for all in-game items including weapons, spells, and usable objects");
            AddGameSystemDescription("SpellCaster", "Handles casting and managing magic spells including targeting and effects");
            AddGameSystemDescription("Creature", "Base class for NPCs and enemies with AI behavior, health, and combat abilities");
            AddGameSystemDescription("LevelLoader", "Manages loading and transitioning between different game levels and scenes");

            Debug.Log($"ChatGPT Mod: Built descriptions for {gameComponentDescriptions.Count} game components");
        }

        private string InferPurpose(string codeInfo)
        {
            // Very simple purpose inference based on class name and contents
            if (codeInfo.Contains("Update") && codeInfo.Contains("Start"))
                return "handling game behavior";
            else if (codeInfo.Contains("Load") || codeInfo.Contains("Save"))
                return "managing data or game state";
            else if (codeInfo.Contains("Damage") || codeInfo.Contains("Health"))
                return "combat or health systems";
            else if (codeInfo.Contains("UI") || codeInfo.Contains("Button"))
                return "user interface elements";
            else if (codeInfo.Contains("Spawn") || codeInfo.Contains("Instantiate"))
                return "creating or managing game objects";
            else if (codeInfo.Contains("Audio") || codeInfo.Contains("Sound"))
                return "audio playback and management";
            else
                return "game functionality";
        }

        private void AddGameSystemDescription(string systemName, string description)
        {
            if (gameComponentDescriptions.ContainsKey(systemName))
                gameComponentDescriptions[systemName] = description;
            else
                gameComponentDescriptions.Add(systemName, description);
        }

        private void UpdateGameComponentCache()
        {
            // Refresh cache with any newly loaded components
            DiscoverGameSystems();
            BuildComponentDescriptions();
        }

        private string GetGameSystemsOverview()
        {
            StringBuilder overview = new StringBuilder();
            overview.AppendLine("Game Systems Overview:");

            // List the top 10 most important systems
            var topSystems = gameComponentDescriptions.Take(10);
            foreach (var system in topSystems)
            {
                overview.AppendLine($"- {system.Key}: {system.Value}");
            }

            return overview.ToString();
        }

        private void CreateCommandUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("CommandCanvas");
            canvasObj.transform.SetParent(transform, false);
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.WorldSpace;

            // Add Canvas Scaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.scaleFactor = 1.0f;
            scaler.dynamicPixelsPerUnit = 100f;

            // Add GraphicRaycaster for input
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create panel background
            commandPanel = new GameObject("CommandPanel");
            commandPanel.transform.SetParent(uiCanvas.transform, false);
            Image panelImage = commandPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            RectTransform panelTransform = commandPanel.GetComponent<RectTransform>();
            panelTransform.anchoredPosition = Vector3.zero;
            panelTransform.sizeDelta = new Vector2(400f, 200f);

            // Output text field - using simple text like original version
            GameObject outputFieldObj = new GameObject("OutputText");
            outputFieldObj.transform.SetParent(panelTransform, false);
            outputText = outputFieldObj.AddComponent<Text>();
            outputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            outputText.fontSize = 14;
            outputText.color = Color.white;
            outputText.text = "ChatGPT AI Assistant is ready! Type a command and press Send.";
            outputText.alignment = TextAnchor.UpperLeft; // Changed to upper left for better scrolling
            outputText.verticalOverflow = VerticalWrapMode.Overflow; // Allow text to overflow for scrolling

            RectTransform outputTransform = outputFieldObj.GetComponent<RectTransform>();
            outputTransform.anchoredPosition = new Vector2(0, 10);
            outputTransform.sizeDelta = new Vector2(350f, 60f);

            // Set proper anchors for scrolling text
            outputTransform.anchorMin = new Vector2(0.5f, 1); // Top center
            outputTransform.anchorMax = new Vector2(0.5f, 1);
            outputTransform.pivot = new Vector2(0.5f, 1); // Pivot at top center

            // Input field
            GameObject inputObj = new GameObject("InputField");
            inputObj.transform.SetParent(panelTransform, false);
            commandInput = inputObj.AddComponent<InputField>();

            // Input field background
            Image inputImage = inputObj.AddComponent<Image>();
            inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1.0f);

            // Input text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            Text inputText = textObj.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            inputText.fontSize = 14;
            inputText.color = Color.white;

            RectTransform textTransform = textObj.GetComponent<RectTransform>();
            textTransform.anchorMin = new Vector2(0, 0);
            textTransform.anchorMax = new Vector2(1, 1);
            textTransform.offsetMin = new Vector2(10, 6);
            textTransform.offsetMax = new Vector2(-10, -7);

            commandInput.textComponent = inputText;

            // Input placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            Text placeholder = placeholderObj.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholder.fontSize = 14;
            placeholder.color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
            placeholder.text = "Enter a command...";

            RectTransform placeholderTransform = placeholderObj.GetComponent<RectTransform>();
            placeholderTransform.anchorMin = new Vector2(0, 0);
            placeholderTransform.anchorMax = new Vector2(1, 1);
            placeholderTransform.offsetMin = new Vector2(10, 6);
            placeholderTransform.offsetMax = new Vector2(-10, -7);

            commandInput.placeholder = placeholder;

            RectTransform inputTransform = inputObj.GetComponent<RectTransform>();
            inputTransform.anchoredPosition = new Vector2(0, 60);
            inputTransform.sizeDelta = new Vector2(350, 30);

            // Send button
            GameObject sendButtonObj = new GameObject("SendButton");
            sendButtonObj.transform.SetParent(panelTransform, false);
            Button sendButton = sendButtonObj.AddComponent<Button>();
            sendButton.onClick.AddListener(SendCommand);

            Image sendButtonImage = sendButtonObj.AddComponent<Image>();
            sendButtonImage.color = new Color(0.2f, 0.6f, 0.2f);

            RectTransform sendButtonRect = sendButtonObj.GetComponent<RectTransform>();
            sendButtonRect.anchoredPosition = new Vector2(-120, -50);
            sendButtonRect.sizeDelta = new Vector2(90, 30);

            GameObject sendButtonTextObj = new GameObject("Text");
            sendButtonTextObj.transform.SetParent(sendButtonObj.transform, false);
            Text sendButtonText = sendButtonTextObj.AddComponent<Text>();
            sendButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            sendButtonText.fontSize = 14;
            sendButtonText.alignment = TextAnchor.MiddleCenter;
            sendButtonText.text = "Send";
            sendButtonText.color = Color.white;

            RectTransform sendButtonTextRect = sendButtonTextObj.GetComponent<RectTransform>();
            sendButtonTextRect.anchorMin = Vector2.zero;
            sendButtonTextRect.anchorMax = Vector2.one;
            sendButtonTextRect.offsetMin = Vector2.zero;
            sendButtonTextRect.offsetMax = Vector2.zero;

            // Toggle UI button
            GameObject toggleUIButtonObj = new GameObject("ToggleButton");
            toggleUIButtonObj.transform.SetParent(panelTransform, false);
            Button toggleUIButton = toggleUIButtonObj.AddComponent<Button>();
            toggleUIButton.onClick.AddListener(ToggleUI);

            Image toggleUIButtonImage = toggleUIButtonObj.AddComponent<Image>();
            toggleUIButtonImage.color = new Color(0.4f, 0.4f, 0.4f);

            RectTransform toggleUIButtonRect = toggleUIButtonObj.GetComponent<RectTransform>();
            toggleUIButtonRect.anchoredPosition = new Vector2(0, -50);
            toggleUIButtonRect.sizeDelta = new Vector2(90, 30);

            GameObject toggleUIButtonTextObj = new GameObject("Text");
            toggleUIButtonTextObj.transform.SetParent(toggleUIButtonObj.transform, false);
            Text toggleUIButtonText = toggleUIButtonTextObj.AddComponent<Text>();
            toggleUIButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            toggleUIButtonText.fontSize = 14;
            toggleUIButtonText.alignment = TextAnchor.MiddleCenter;
            toggleUIButtonText.text = "Hide";
            toggleUIButtonText.color = Color.white;

            RectTransform toggleUIButtonTextRect = toggleUIButtonTextObj.GetComponent<RectTransform>();
            toggleUIButtonTextRect.anchorMin = Vector2.zero;
            toggleUIButtonTextRect.anchorMax = Vector2.one;
            toggleUIButtonTextRect.offsetMin = Vector2.zero;
            toggleUIButtonTextRect.offsetMax = Vector2.zero;

            // Offline mode button
            GameObject offlineButtonObj = new GameObject("OfflineButton");
            offlineButtonObj.transform.SetParent(panelTransform, false);
            Button offlineButton = offlineButtonObj.AddComponent<Button>();
            offlineButton.onClick.AddListener(ToggleOfflineMode);

            Image offlineButtonImage = offlineButtonObj.AddComponent<Image>();
            // Set color based on ACTUAL current mode
            offlineButtonImage.color = offlineMode ? new Color(0.8f, 0.4f, 0.4f) : new Color(0.4f, 0.4f, 0.8f);

            RectTransform offlineButtonRect = offlineButtonObj.GetComponent<RectTransform>();
            offlineButtonRect.anchoredPosition = new Vector2(120, -50);
            offlineButtonRect.sizeDelta = new Vector2(90, 30);

            GameObject offlineButtonTextObj = new GameObject("Text");
            offlineButtonTextObj.transform.SetParent(offlineButtonObj.transform, false);
            Text offlineButtonText = offlineButtonTextObj.AddComponent<Text>();
            offlineButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            offlineButtonText.fontSize = 14;
            offlineButtonText.alignment = TextAnchor.MiddleCenter;
            // Set text based on ACTUAL current mode - this was backward in original code
            offlineButtonText.text = offlineMode ? "Offline" : "Online";
            offlineButtonText.color = Color.white;

            RectTransform offlineButtonTextRect = offlineButtonTextObj.GetComponent<RectTransform>();
            offlineButtonTextRect.anchorMin = Vector2.zero;
            offlineButtonTextRect.anchorMax = Vector2.one;
            offlineButtonTextRect.offsetMin = Vector2.zero;
            offlineButtonTextRect.offsetMax = Vector2.zero;

            // Reset button
            GameObject resetButtonObj = new GameObject("ResetButton");
            resetButtonObj.transform.SetParent(panelTransform, false);
            Button resetButton = resetButtonObj.AddComponent<Button>();
            resetButton.onClick.AddListener(ResetCommandState);

            Image resetButtonImage = resetButtonObj.AddComponent<Image>();
            resetButtonImage.color = new Color(1.0f, 0.5f, 0.0f);

            RectTransform resetButtonRect = resetButtonObj.GetComponent<RectTransform>();
            resetButtonRect.anchoredPosition = new Vector2(0, -85);
            resetButtonRect.sizeDelta = new Vector2(90, 30);

            GameObject resetButtonTextObj = new GameObject("Text");
            resetButtonTextObj.transform.SetParent(resetButtonObj.transform, false);
            Text resetButtonText = resetButtonTextObj.AddComponent<Text>();
            resetButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            resetButtonText.fontSize = 14;
            resetButtonText.alignment = TextAnchor.MiddleCenter;
            resetButtonText.text = "Reset";
            resetButtonText.color = Color.white;

            RectTransform resetButtonTextRect = resetButtonTextObj.GetComponent<RectTransform>();
            resetButtonTextRect.anchorMin = Vector2.zero;
            resetButtonTextRect.anchorMax = Vector2.one;
            resetButtonTextRect.offsetMin = Vector2.zero;
            resetButtonTextRect.offsetMax = Vector2.zero;

            // Advanced Mode button (bottom left)
            GameObject advancedButtonObj = new GameObject("AdvancedButton");
            advancedButtonObj.transform.SetParent(panelTransform, false);
            Button advancedButton = advancedButtonObj.AddComponent<Button>();
            advancedButton.onClick.AddListener(ToggleAdvancedMode);

            Image advancedButtonImage = advancedButtonObj.AddComponent<Image>();
            advancedButtonImage.color = isAdvancedMode ? new Color(0.6f, 0.3f, 0.7f) : new Color(0.3f, 0.3f, 0.7f);

            RectTransform advancedButtonRect = advancedButtonObj.GetComponent<RectTransform>();
            advancedButtonRect.anchoredPosition = new Vector2(-120, -85);
            advancedButtonRect.sizeDelta = new Vector2(90, 30);

            GameObject advancedButtonTextObj = new GameObject("Text");
            advancedButtonTextObj.transform.SetParent(advancedButtonObj.transform, false);
            Text advancedButtonText = advancedButtonTextObj.AddComponent<Text>();
            advancedButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            advancedButtonText.fontSize = 14;
            advancedButtonText.alignment = TextAnchor.MiddleCenter;
            advancedButtonText.text = isAdvancedMode ? "Basic" : "Advanced";
            advancedButtonText.color = Color.white;

            RectTransform advancedButtonTextRect = advancedButtonTextObj.GetComponent<RectTransform>();
            advancedButtonTextRect.anchorMin = Vector2.zero;
            advancedButtonTextRect.anchorMax = Vector2.one;
            advancedButtonTextRect.offsetMin = Vector2.zero;
            advancedButtonTextRect.offsetMax = Vector2.zero;

            // Status text below the buttons
            GameObject statusTextObj = new GameObject("StatusText");
            statusTextObj.transform.SetParent(panelTransform, false);
            statusText = statusTextObj.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 12;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.text = "Ready";

            RectTransform statusTextRect = statusTextObj.GetComponent<RectTransform>();
            statusTextRect.anchoredPosition = new Vector2(0, -115);
            statusTextRect.sizeDelta = new Vector2(300, 30);

            // Add scroll up button - DIRECT IMPLEMENTATION
            GameObject scrollUpBtnObj = new GameObject("ScrollUpButton");
            scrollUpBtnObj.transform.SetParent(panelTransform, false);
            Button scrollUpBtn = scrollUpBtnObj.AddComponent<Button>();

            // Use a lambda to avoid dependency on class method that uses chatScrollRect
            scrollUpBtn.onClick.AddListener(() => {
                Debug.Log("ScrollUp button clicked - direct implementation");
                // Move text up (scrolls view down)
                RectTransform txtRect = outputText.GetComponent<RectTransform>();
                Vector2 pos = txtRect.anchoredPosition;
                pos.y += 30f; // Move text up by 30 units
                txtRect.anchoredPosition = pos;
                Debug.Log($"New text Y position: {pos.y}");
            });

            Image scrollUpBtnImage = scrollUpBtnObj.AddComponent<Image>();
            scrollUpBtnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            RectTransform scrollUpBtnRect = scrollUpBtnObj.GetComponent<RectTransform>();
            scrollUpBtnRect.anchoredPosition = new Vector2(180, 60); // Right side, above center
            scrollUpBtnRect.sizeDelta = new Vector2(40, 40); // Make it a good size

            // Add up arrow text
            GameObject scrollUpTextObj = new GameObject("Text");
            scrollUpTextObj.transform.SetParent(scrollUpBtnObj.transform, false);
            Text scrollUpText = scrollUpTextObj.AddComponent<Text>();
            scrollUpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            scrollUpText.fontSize = 20;
            scrollUpText.alignment = TextAnchor.MiddleCenter;
            scrollUpText.text = "▲";
            scrollUpText.color = Color.white;

            RectTransform scrollUpTextRect = scrollUpTextObj.GetComponent<RectTransform>();
            scrollUpTextRect.anchorMin = Vector2.zero;
            scrollUpTextRect.anchorMax = Vector2.one;
            scrollUpTextRect.offsetMin = Vector2.zero;
            scrollUpTextRect.offsetMax = Vector2.zero;

            // Add scroll down button - DIRECT IMPLEMENTATION
            GameObject scrollDownBtnObj = new GameObject("ScrollDownButton");
            scrollDownBtnObj.transform.SetParent(panelTransform, false);
            Button scrollDownBtn = scrollDownBtnObj.AddComponent<Button>();

            // Use a lambda to avoid dependency on class method that uses chatScrollRect
            scrollDownBtn.onClick.AddListener(() => {
                Debug.Log("ScrollDown button clicked - direct implementation");
                // Move text down (scrolls view up)
                RectTransform txtRect = outputText.GetComponent<RectTransform>();
                Vector2 pos = txtRect.anchoredPosition;
                pos.y -= 30f; // Move text down by 30 units
                txtRect.anchoredPosition = pos;
                Debug.Log($"New text Y position: {pos.y}");
            });

            Image scrollDownBtnImage = scrollDownBtnObj.AddComponent<Image>();
            scrollDownBtnImage.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

            RectTransform scrollDownBtnRect = scrollDownBtnObj.GetComponent<RectTransform>();
            scrollDownBtnRect.anchoredPosition = new Vector2(180, -40); // Right side, below center
            scrollDownBtnRect.sizeDelta = new Vector2(40, 40); // Make it a good size

            // Add down arrow text
            GameObject scrollDownTextObj = new GameObject("Text");
            scrollDownTextObj.transform.SetParent(scrollDownBtnObj.transform, false);
            Text scrollDownText = scrollDownTextObj.AddComponent<Text>();
            scrollDownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            scrollDownText.fontSize = 20;
            scrollDownText.alignment = TextAnchor.MiddleCenter;
            scrollDownText.text = "▼";
            scrollDownText.color = Color.white;

            RectTransform scrollDownTextRect = scrollDownTextObj.GetComponent<RectTransform>();
            scrollDownTextRect.anchorMin = Vector2.zero;
            scrollDownTextRect.anchorMax = Vector2.one;
            scrollDownTextRect.offsetMin = Vector2.zero;
            scrollDownTextRect.offsetMax = Vector2.zero;

            // Position the panel in front of the player using correct scaling!
            UpdatePanelPosition();

            // Set up input field to submit on Enter key
            commandInput.onEndEdit.AddListener(delegate (string value) {
                try
                {
                    if (value.Length > 0 &&
                        (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    {
                        SendCommand();
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Input error in onEndEdit: {ex.Message}");
                }
            });

            // Log that we've created the UI with direct scroll buttons
            Debug.Log("CreateCommandUI complete - direct scroll buttons implemented");
        }

        private void Update()
        {
            try
            {
                // Toggle UI visibility with a key press (F key)
                if (Input.GetKeyDown(KeyCode.F))
                {
                    ToggleUI();
                }

                // Quick toggle between offline and online modes (O key)
                if (Input.GetKeyDown(KeyCode.O))
                {
                    ToggleOfflineMode();
                }

                // Force reset of command state if R key is pressed
                if (Input.GetKeyDown(KeyCode.R))
                {
                    ResetCommandState();
                }

                // Toggle advanced mode with A key
                if (Input.GetKeyDown(KeyCode.A))
                {
                    ToggleAdvancedMode();
                }
            }
            catch (System.Exception ex)
            {
                // Log any input errors but don't crash
                Debug.LogWarning($"Input error: {ex.Message}");
            }

            if (chatScrollRect != null && IsPointerOverUI())
            {
                float scrollDelta = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    chatScrollRect.verticalNormalizedPosition += scrollDelta * 0.1f;
                    chatScrollRect.verticalNormalizedPosition = Mathf.Clamp01(chatScrollRect.verticalNormalizedPosition);
                }
            }
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void ScrollToBottom()
        {
            // Wait for layout to update then scroll
            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            // Wait for end of frame
            yield return new WaitForEndOfFrame();

            // Scroll to bottom
            if (chatScrollRect != null)
            {
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void UpdatePanelPosition()
        {
            if (uiCanvas != null)
            {
                Transform cameraTransform = Camera.main.transform;
                uiCanvas.transform.position = cameraTransform.position + cameraTransform.forward * 0.5f;
                uiCanvas.transform.rotation = cameraTransform.rotation;

                // THIS IS THE KEY CHANGE - use the BASE_SCALE factor!
                uiCanvas.transform.localScale = Vector3.one * BASE_SCALE * uiScale;
            }
        }

        public void RefreshPanelPosition()
        {
            Debug.Log("ChatGPT Spell Controller: RefreshPanelPosition called");

            if (commandPanel != null)
            {
                // First, make sure the panel is active
                commandPanel.SetActive(true);

                Canvas canvas = commandPanel.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    // Make sure canvas is visible
                    canvas.enabled = true;

                    // Position in front of player's head
                    if (Player.local != null && Player.local.head != null)
                    {
                        canvas.transform.position = Player.local.head.transform.position +
                                                  Player.local.head.transform.forward * 0.5f;
                        canvas.transform.rotation = Player.local.head.transform.rotation;

                        // Use the correct scaling!
                        canvas.transform.localScale = Vector3.one * BASE_SCALE * uiScale;

                        Debug.Log($"ChatGPT Spell Controller: Panel repositioned in front of player at {canvas.transform.position}");
                    }
                    else
                    {
                        Debug.LogWarning("ChatGPT Spell Controller: Player or player head not found");

                        // Fallback positioning if player not found yet
                        Camera mainCamera = Camera.main;
                        if (mainCamera != null)
                        {
                            canvas.transform.position = mainCamera.transform.position +
                                                      mainCamera.transform.forward * 0.5f;
                            canvas.transform.rotation = mainCamera.transform.rotation;

                            // Use the correct scaling!
                            canvas.transform.localScale = Vector3.one * BASE_SCALE * uiScale;

                            Debug.Log("ChatGPT Spell Controller: Panel positioned using main camera");
                        }
                    }

                    // Force redraw to ensure visibility
                    Canvas.ForceUpdateCanvases();

                    // Focus input field
                    if (commandInput != null)
                    {
                        commandInput.ActivateInputField();
                    }
                }
                else
                {
                    Debug.LogError("ChatGPT Spell Controller: Canvas component not found");
                }

                // Make sure it's still active
                commandPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("ChatGPT Spell Controller: commandPanel is null");
            }
        }

        public void ResetUI()
        {
            Debug.Log("ChatGPT Spell Controller: Resetting UI");

            // Destroy the old UI if it exists
            Canvas oldCanvas = GetComponentInChildren<Canvas>();
            if (oldCanvas != null)
            {
                Debug.Log("ChatGPT Spell Controller: Destroying old canvas");
                Destroy(oldCanvas.gameObject);
            }

            // Wait a frame to ensure destruction completes
            StartCoroutine(RecreateUIAfterDelay());
        }

        private IEnumerator RecreateUIAfterDelay()
        {
            yield return null;

            // Recreate the UI
            Debug.Log("ChatGPT Spell Controller: Recreating UI");
            CreateCommandUI();

            // Make sure it's visible
            if (commandPanel != null)
            {
                commandPanel.SetActive(true);
                UpdatePanelPosition();
            }
        }

        public void ResetCommandState()
        {
            apiCallInProgress = false;
            UpdateStatus("Ready - State Reset");
            Debug.Log("ChatGPT Spell Controller: Command state forcibly reset");
            ForceResetState();
        }

        private void UpdateStatus(string message)
        {
            currentStatus = message;
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"ChatGPT Spell Controller: Status - {message}");
        }

        public void SendCommand()
        {
            string command = commandInput.text;
            if (string.IsNullOrEmpty(command))
            {
                UpdateStatus("Command cannot be empty");
                return;
            }

            if (apiCallInProgress)
            {
                UpdateStatus("Still processing previous command...");
                return;
            }

            // Clear the input field and prepare for the next command
            commandInput.text = "";
            Debug.Log($"Processing command: '{command}', Current mode: {(offlineMode ? "OFFLINE" : "ONLINE")}");
            UpdateStatus($"Processing: {command}");

            // Handle special commands that should always work regardless of mode
            string lowerCmd = command.ToLower();

            // API Key command should always work in any mode
            if (lowerCmd.StartsWith("/apikey "))
            {
                string newKey = command.Substring(8).Trim();
                if (!string.IsNullOrEmpty(newKey))
                {
                    SetApiKey(newKey);
                    outputText.text = "API Key updated. You can now use online mode.";
                    return;
                }
                else
                {
                    outputText.text = "Please provide a valid API key after /apikey";
                    return;
                }
            }

            // Other special commands that should work in any mode
            if (lowerCmd == "/help")
            {
                DisplayHelpInformation();
                return;
            }
            else if (lowerCmd == "/online")
            {
                ForceOnlineMode();
                return;
            }
            else if (lowerCmd.StartsWith("/scale "))
            {
                HandleScaleCommand(command);
                return;
            }
            // Process remaining special commands that might be mode-dependent
            else if (ProcessSpecialCommands(command))
            {
                return;
            }

            // Now handle normal commands based on mode
            if (offlineMode)
            {
                ProcessOfflineCommand(command);
            }
            else
            {
                StartCoroutine(ProcessCommandWithTimeout(command));
            }
        }

        // Add a helper method for scale command
        private void HandleScaleCommand(string command)
        {
            try
            {
                string scaleStr = command.Substring(7).Trim();
                float newScale = float.Parse(scaleStr);
                if (newScale > 0.01f && newScale < 10.0f)
                {
                    uiScale = newScale;
                    UpdatePanelPosition();
                    SaveConfig();
                    UpdateStatus($"UI Scale set to: {uiScale:F2}");
                    outputText.text = $"UI Scale set to: {uiScale:F2}";
                }
                else
                {
                    UpdateStatus("Scale must be between 0.01 and 10.0");
                    outputText.text = "Scale must be between 0.01 and 10.0";
                }
            }
            catch
            {
                UpdateStatus("Invalid scale value");
                outputText.text = "Invalid scale value";
            }
        }

        void InitializeScriptGenerator()
        {
            scriptGenerator = GetComponent<ScriptableGenerator>();
            if (scriptGenerator == null)
            {
                scriptGenerator = gameObject.AddComponent<ScriptableGenerator>();
                Debug.Log("Added ScriptableGenerator component to ChatGPT Controller");
            }
        }
        private bool HandleScriptGeneration(string command)
        {
            string lowerCommand = command.ToLower();

            if (lowerCommand.StartsWith("/generate ") || lowerCommand.StartsWith("/add "))
            {
                string request = lowerCommand.StartsWith("/generate ") ?
                    command.Substring(10).Trim() : // "/generate " is 10 characters
                    command.Substring(5).Trim();  // "/add " is 5 characters

                Debug.Log($"Activating ScriptGenerator with request: {request}");

                // Initialize if needed
                if (scriptGenerator == null)
                {
                    InitializeScriptGenerator();
                }

                if (scriptGenerator != null)
                {
                    // Start the script generation process
                    StartCoroutine(ProcessScriptGeneration(scriptGenerator, request));
                    return true; // Command was handled
                }
                else
                {
                    outputText.text = "Error: ScriptableGenerator component not found!";
                    UpdateStatus("ScriptableGenerator component not found!");
                    Debug.LogError("ScriptableGenerator component not found!");
                    return true; // Command was handled (with error)
                }
            }

            return false; // Command not handled
        }



        private bool ProcessSpecialCommands(string command)
        {
            string lowerCommand = command.ToLower();
            Debug.Log($"Processing special command: {command}, OfflineMode: {offlineMode}");


            // Skip commands that are already handled in SendCommand
            if (lowerCommand.StartsWith("/apikey ") ||
                lowerCommand == "/help" ||
                lowerCommand == "/online" ||
                lowerCommand.StartsWith("/scale "))
            {
                return false;
            }

            else if (lowerCommand.StartsWith("/generate ") || lowerCommand.StartsWith("/add "))
            {
                string request = lowerCommand.StartsWith("/generate ") ?
                    command.Substring(10).Trim() : // "/generate " is 10 characters
                    command.Substring(5).Trim();  // "/add " is 5 characters

                Debug.Log($"Activating ScriptGenerator with request: {request}");

                if (scriptGenerator != null)
                {
                    // Start the script generation process
                    StartCoroutine(ProcessScriptGeneration(scriptGenerator, request));
                    return true; // Command was handled
                }
                else
                {
                    outputText.text = "Error: ScriptableGenerator component not found!";
                    UpdateStatus("ScriptableGenerator component not found!");
                    Debug.LogError("ScriptableGenerator component not found!");
                    return true; // Command was handled (with error)
                }
            }

            // Check for other special command prefixes
            if (lowerCommand.StartsWith("/"))
            {
                Debug.Log($"Found special command: {lowerCommand}");

                // ADD THESE NEW COMMANDS FOR SELF-MODIFYING AI
                if (lowerCommand.StartsWith("/modify ") || lowerCommand.StartsWith("/code "))
                {
                    // Extract the actual request (remove the command prefix)
                    string request = lowerCommand.StartsWith("/modify ") ?
                        command.Substring(8).Trim() : // "/modify " is 8 characters
                        command.Substring(6).Trim();  // "/code " is 6 characters

                    Debug.Log($"Activating SelfModifyingAI with request: {request}");

                    // Get the SelfModifyingAI component that was added in Awake
                    SelfModifyingAI modAI = GetComponent<SelfModifyingAI>();
                    if (modAI != null)
                    {
                        // Start the modification process
                        StartCoroutine(ProcessCodeModification(modAI, request));
                        return true; // Command was handled
                    }
                    else
                    {
                        outputText.text = "Error: SelfModifyingAI component not found!";
                        UpdateStatus("SelfModifyingAI component not found!");
                        Debug.LogError("SelfModifyingAI component not found!");
                        return true; // Command was handled (with error)
                    }
                }
                // ORIGINAL CODE CONTINUES BELOW
                else if (lowerCommand.StartsWith("/analyze "))
                {
                    string componentName = command.Substring(9).Trim();
                    AnalyzeSpecificComponent(componentName);
                    return true;
                }
                else if (lowerCommand == "/systems")
                {
                    DisplayGameSystems();
                    return true;
                }
                else if (lowerCommand.StartsWith("/model "))
                {
                    string newModel = command.Substring(7).Trim().ToLower();
                    if (newModel == "gpt-3.5-turbo" || newModel == "gpt-4" ||
                        newModel == "gpt-4-turbo" || newModel == "gpt-3.5")
                    {
                        ChangeModel(newModel);
                        return true;
                    }
                }
                else if (lowerCommand.StartsWith("/saveconfig"))
                {
                    SaveConfig();
                    UpdateStatus("Configuration saved successfully");
                    outputText.text = "Configuration saved successfully";
                    return true;
                }
                else if (lowerCommand.StartsWith("/loadconfig"))
                {
                    LoadConfig();
                    UpdateStatus("Configuration loaded successfully");
                    outputText.text = "Configuration loaded successfully";
                    return true;
                }
                else if (lowerCommand == "/clear")
                {
                    ClearConversationHistory();
                    return true;
                }
            }

            return false;
        }

        private IEnumerator ProcessScriptGeneration(ScriptableGenerator generator, string request)
        {
            // Update UI to show we're processing
            UpdateStatus("Generating new script based on request...");
            outputText.text = "Generating new script based on request...";

            // Call the ScriptableGenerator to process the request
            System.Threading.Tasks.Task<string> task = generator.GenerateAndAddScript(request);

            // Wait until the task completes
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Get the result and display it
            string result = task.Result;
            outputText.text = result;
            UpdateStatus("Script generation complete");

            Debug.Log($"Script generation result: {result}");
        }

        private IEnumerator ProcessCodeModification(SelfModifyingAI modAI, string request)
        {
            // Update UI to show we're processing
            UpdateStatus("Processing code modification request...");
            outputText.text = "Processing code modification request...";

            // Call the SelfModifyingAI to process the request
            System.Threading.Tasks.Task<string> task = modAI.ProcessCodeModificationRequest(request);

            // Wait until the task completes
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Get the result and display it
            string result = task.Result;
            outputText.text = result;
            UpdateStatus("Code modification complete");

            Debug.Log($"SelfModifyingAI result: {result}");
        }
        private IEnumerator ModifyCodeAndReturnResult(SelfModifyingAI modAI, string request)
        {
            // Show a processing message

            // Start the async operation
            Task<string> task = modAI.ProcessCodeModificationRequest(request);

            // Wait until complete (could add timeout logic here)
            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Get and display the result
            string result = task.Result;
        }

        public void ForceResetState()
        {
            apiCallInProgress = false;
            offlineMode = false;

            UpdateStatus("Ready - Complete State Reset");

            // Clear any pending coroutines
            StopAllCoroutines();

            // Reset UI indicators for offline mode
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Image buttonImage = offlineButton.GetComponent<Image>();
                Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    buttonImage.color = new Color(0.4f, 0.4f, 0.8f); // Online color (blue)
                    buttonText.text = "Online";
                }
            }

            Debug.Log("ChatGPT Spell Controller: All state forcibly reset, mode set to ONLINE");
        }

        // Update ResetCommandState to call ForceResetState
    

        private void AnalyzeSpecificComponent(string componentName)
        {
            if (string.IsNullOrEmpty(componentName))
            {
                outputText.text = "Please specify a component name to analyze.";
                return;
            }

            UpdateStatus($"Analyzing component: {componentName}...");

            // Check if we have info on this component
            if (gameCodeCache.TryGetValue(componentName, out string componentInfo))
            {
                outputText.text = $"Analysis of {componentName}:\n\n{componentInfo}";
                analyzedComponents.Add(componentName);

                // Add to conversation history for context
                conversationHistory.Add(new Message
                {
                    role = "system",
                    content = $"User has analyzed the {componentName} component with the following details:\n{componentInfo}"
                });
            }
            else
            {
                // Try to find similar component names
                List<string> similarComponents = new List<string>();
                foreach (var component in gameCodeCache.Keys)
                {
                    if (component.ToLower().Contains(componentName.ToLower()))
                    {
                        similarComponents.Add(component);
                    }
                }

                if (similarComponents.Count > 0)
                {
                    outputText.text = $"Component '{componentName}' not found exactly. Did you mean one of these?\n\n";
                    foreach (var comp in similarComponents.Take(10))
                    {
                        outputText.text += $"- {comp}\n";
                    }
                }
                else
                {
                    outputText.text = $"Component '{componentName}' not found in the analyzed game code.";
                }
            }
        }

        private void DisplayGameSystems()
        {
            string systemsOverview = GetGameSystemsOverview();
            outputText.text = systemsOverview;
            UpdateStatus("Displaying game systems overview");
        }

        private void ChangeModel(string newModel)
        {
            modelName = newModel;
            outputText.text = $"Model changed to: {newModel}";
            UpdateStatus($"Using model: {newModel}");

            // Update config
            SaveConfig();
        }

        private void DisplayHelpInformation()
        {
            StringBuilder helpInfo = new StringBuilder();

            // Add authentication-specific help
            helpInfo.AppendLine("API Key Commands:");
            helpInfo.AppendLine("/apikey <your-key> - Set your OpenAI API key");
            helpInfo.AppendLine("/verify - Check if your API key format appears valid");
            helpInfo.AppendLine("/test - Test your API key with the OpenAI API");
            helpInfo.AppendLine("");
            helpInfo.AppendLine("ChatGPT AI Assistant Help");
            helpInfo.AppendLine("");
            helpInfo.AppendLine("Special Commands:");
            helpInfo.AppendLine("/analyze <component> - Analyze a specific game component");
            helpInfo.AppendLine("/systems - Show overview of game systems");
            helpInfo.AppendLine("/model <name> - Change AI model (gpt-3.5-turbo, gpt-4)");
            helpInfo.AppendLine("/scale <number> - Set UI scale (0.1-10.0)");
            helpInfo.AppendLine("/clear - Clear conversation history");
            helpInfo.AppendLine("/help - Show this help information");
            helpInfo.AppendLine("/saveconfig - Save current configuration");
            helpInfo.AppendLine("/loadconfig - Load saved configuration");
            helpInfo.AppendLine("");
            helpInfo.AppendLine("Keyboard Shortcuts:");
            helpInfo.AppendLine("F - Toggle UI visibility");
            helpInfo.AppendLine("O - Toggle offline/online mode");
            helpInfo.AppendLine("R - Reset command state");
            helpInfo.AppendLine("A - Toggle advanced mode");
            helpInfo.AppendLine("");
            helpInfo.AppendLine("Example Queries:");
            helpInfo.AppendLine("- How does the damage system work?");
            helpInfo.AppendLine("- Write code to create a new spell that freezes enemies");
            helpInfo.AppendLine("- Explain how item spawning works in this game");
            helpInfo.AppendLine("- How can I modify the player movement speed?");
            helpInfo.AppendLine("- Summon a fire sword");
            helpInfo.AppendLine("/generate <description> - Generate a new script based on description");
            helpInfo.AppendLine("/add <description> - Alternative command to add a new script");
            helpInfo.AppendLine("");
            helpInfo.AppendLine("Script Generation Examples:");
            helpInfo.AppendLine("- /generate a fireball spell that explodes on impact");
            helpInfo.AppendLine("- /add an enemy AI that patrols and attacks the player");


            outputText.text = helpInfo.ToString();
            UpdateStatus("Help information displayed");
        }

        private void ClearConversationHistory()
        {
            // Save system message
            Message systemMessage = conversationHistory[0];

            // Clear history
            conversationHistory.Clear();

            // Re-add system message
            conversationHistory.Add(systemMessage);

            outputText.text = "Conversation history cleared. System context maintained.";
            UpdateStatus("Conversation history cleared");

            // Save the cleared history
            SaveConversationHistory();
        }

        private IEnumerator ProcessCommandWithTimeout(string command)
        {
            apiCallInProgress = true;
            bool completed = false;
            string response = null;
            System.Exception error = null;

            // If in advanced mode, enrich the command with game context
            string enrichedCommand = isAdvancedMode ? EnrichCommandWithContext(command) : command;


            // Start API call in a separate thread
            System.Threading.Tasks.Task.Run(async () => {
                try
                {
                    response = await SendCommandToOpenAI(enrichedCommand);
                    completed = true;
                }
                catch (System.Exception e)
                {
                    error = e;
                    completed = true;
                }
            });
            

            // Wait for completion or timeout
            float startTime = Time.time;
            while (!completed && Time.time - startTime < apiTimeoutDuration)
            {
                // Update status with elapsed time
                UpdateStatus($"Processing: {command} ({(Time.time - startTime):F1}s)");
                yield return new WaitForSeconds(0.2f);
            }

            // If timed out
            if (!completed)
            {
                UpdateStatus("Request timed out. Switching to offline mode...");

                // IMPORTANT CHANGE: Actually set offline mode
                SwitchToOfflineMode("Request timed out");

                ProcessOfflineCommand(command);
                yield break;
            }


            // If error occurred
            if (error != null)
            {
                string errorMsg = error.Message;
                Debug.LogError($"ChatGPT API Error: {errorMsg}");
                UpdateStatus($"API Error: {errorMsg}");

                // IMPORTANT CHANGE: Actually set offline mode
                SwitchToOfflineMode($"API Error: {errorMsg}");

                ProcessOfflineCommand(command);
                yield break;
            }

            // Process successful response
            outputText.text = response;
            Debug.Log($"ChatGPT Response: {response}");
            apiCallInProgress = false;
            UpdateStatus("Ready");

            // Save conversation history
            SaveConversationHistory();

            // Try to process item spawning request based on command
            TrySpawnItemBasedOnCommand(command);
        }

       
        private bool IsCodeModificationRequest(string input)
        {
            string lowerInput = input.ToLower();

            // Check for code modification keywords
            return lowerInput.Contains("modify code") ||
                   lowerInput.Contains("change code") ||
                   lowerInput.Contains("edit code") ||
                   lowerInput.Contains("create a function") ||
                   lowerInput.Contains("implement a feature") ||
                   lowerInput.Contains("add feature") ||
                   (lowerInput.Contains("code") &&
                    (lowerInput.Contains("write") || lowerInput.Contains("fix") || lowerInput.Contains("update")));
        }
        public async Task<bool> TryCodeModification(string prompt)
        {
            Debug.Log("Attempting code modification via SelfModifyingAI");

            // Get or add the SelfModifyingAI component
            SelfModifyingAI codeModifier = GetComponent<SelfModifyingAI>();
            if (codeModifier == null)
            {
                Debug.LogError("SelfModifyingAI component not found!");
                return false;
            }

            try
            {
                // Explicitly call the code modification process with the user's prompt
                string result = await codeModifier.ProcessCodeModificationRequest(prompt);

                

                Debug.Log("Code modification completed: " + result);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Code modification failed: {ex.Message}");
                return false;
            }
        }

        private void SwitchToOfflineMode(string reason)
        {
            offlineMode = true;
            Debug.Log($"Switching to OFFLINE mode due to: {reason}");

            // Update button to reflect offline mode
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Image buttonImage = offlineButton.GetComponent<Image>();
                Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    buttonImage.color = new Color(0.8f, 0.4f, 0.4f); // Offline color (red)
                    buttonText.text = "Offline";
                }
            }
        }


        private string EnrichCommandWithContext(string command)
        {
            StringBuilder enrichedCommand = new StringBuilder(command);

            // Check what the command is about
            string lowerCommand = command.ToLower();

            // Don't enrich simple spawn commands
            if (lowerCommand.Contains("summon") || lowerCommand.Contains("spawn"))
            {
                return command;
            }

            // Check if command is asking about game systems
            foreach (var system in gameComponentDescriptions.Keys)
            {
                if (lowerCommand.Contains(system.ToLower()))
                {
                    // Add context about this system if we have it
                    if (gameCodeCache.TryGetValue(system, out string codeInfo))
                    {
                        enrichedCommand.Append("\n\n[System Context: ");
                        enrichedCommand.Append(gameComponentDescriptions[system]);
                        enrichedCommand.Append("\n\nBrief code structure: ");

                        // Add a simplified version of the code structure
                        string simplifiedCode = codeInfo.Replace("\n", " ").Replace("  ", " ");
                        if (simplifiedCode.Length > 300)
                            simplifiedCode = simplifiedCode.Substring(0, 300) + "...";

                        enrichedCommand.Append(simplifiedCode);
                        enrichedCommand.Append("]");

                        Debug.Log($"Enriched command with context for system: {system}");
                        break; // Only enrich with one system context
                    }
                }
            }

            return enrichedCommand.ToString();
        }

        private void ProcessOfflineCommand(string command)
        {
            string lowerCommand = command.ToLower();
            UpdateStatus($"Offline Mode: Processing '{command}'");

            // Special command processing
            if (ProcessSpecialCommands(command))
            {
                return;
            }

            if (lowerCommand == "/key")
            {
                DisplayApiKeyStatus();
            }

            if (lowerCommand == "/verify")
            {
                VerifyApiKey();
            }

            if (lowerCommand.Contains("summon") || lowerCommand.Contains("spawn"))
            {
                // Handle summon commands in offline mode
                if (lowerCommand.Contains("sword"))
                {
                    TrySpawnItemBasedOnName("Sword");
                    UpdateStatus("Summoned a sword");
                    outputText.text = "Summoned a sword for you.";
                }
                else if (lowerCommand.Contains("axe"))
                {
                    TrySpawnItemBasedOnName("Axe");
                    UpdateStatus("Summoned an axe");
                    outputText.text = "Summoned an axe for you.";
                }
                else if (lowerCommand.Contains("bow"))
                {
                    TrySpawnItemBasedOnName("Bow");
                    UpdateStatus("Summoned a bow");
                    outputText.text = "Summoned a bow for you.";
                }
                else if (lowerCommand.Contains("staff") || lowerCommand.Contains("wand"))
                {
                    TrySpawnItemBasedOnName("Staff");
                    UpdateStatus("Summoned a staff");
                    outputText.text = "Summoned a staff for you.";
                }
                else if (lowerCommand.Contains("fire") || lowerCommand.Contains("fireball"))
                {
                    // Try to spawn a fire spell
                    TrySpawnItemBasedOnName("Fire");
                    UpdateStatus("Summoned fire magic");
                    outputText.text = "Conjured fire magic for you.";
                }
                else if (lowerCommand.Contains("lightning") || lowerCommand.Contains("shock"))
                {
                    // Try to spawn lightning spell
                    TrySpawnItemBasedOnName("Lightning");
                    UpdateStatus("Summoned lightning magic");
                    outputText.text = "Conjured lightning magic for you.";
                }
                else if (lowerCommand.Contains("gravity"))
                {
                    // Try to spawn gravity spell
                    TrySpawnItemBasedOnName("Gravity");
                    UpdateStatus("Summoned gravity magic");
                    outputText.text = "Conjured gravity magic for you.";
                }
                else if (lowerCommand.Contains("ice"))
                {
                    // Try to spawn ice spell
                    TrySpawnItemBasedOnName("Ice");
                    UpdateStatus("Summoned ice magic");
                    outputText.text = "Conjured ice magic for you.";
                }
                else
                {
                    // Generic item summon
                    TrySpawnItemBasedOnName("Sword");
                    UpdateStatus("Summoned a generic item");
                    outputText.text = "Summoned a generic item for you.";
                }
            }
            else if (lowerCommand.Contains("hide") || lowerCommand.Contains("close"))
            {
                // Hide panel
                ToggleUI();
                UpdateStatus("Panel hidden");
                outputText.text = "Panel hidden.";
            }
            else if (lowerCommand.Contains("how") || lowerCommand.Contains("explain") ||
                     lowerCommand.Contains("what") || lowerCommand.Contains("why"))
            {
                // Questions about the game in offline mode
                outputText.text = "I'm currently in offline mode and can't access detailed game information. " +
                                 "Switch to online mode with the 'Offline' button or 'O' key to get detailed answers about game mechanics.";
                UpdateStatus("Question received in offline mode");
            }
            else if (lowerCommand.Contains("code") || lowerCommand.Contains("script") ||
                     lowerCommand.Contains("modify") || lowerCommand.Contains("change"))
            {
                // Code modification requests in offline mode
                outputText.text = "I'm currently in offline mode and can't provide code modifications. " +
                                 "Switch to online mode with the 'Offline' button or 'O' key to get help with code changes.";
                UpdateStatus("Code modification requested in offline mode");
            }
            else
            {
                // Generic response
                UpdateStatus("Command not recognized in offline mode");
                outputText.text = "I didn't understand that command in offline mode. Try asking for a weapon or spell, or switch to online mode for more capabilities.";
            }
        }

        private void VerifyApiKey()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                outputText.text = "⚠️ API Key Issue: Your API key is empty.\n\nUse the /apikey command to set your OpenAI API key.";
                return;
            }

            if (apiKey == "your-api-key-here")
            {
                outputText.text = "⚠️ API Key Issue: You're using the default placeholder API key.\n\nUse the /apikey command to set your actual OpenAI API key.";
                return;
            }

            // Check for common formatting issues
            if (!apiKey.StartsWith("sk-"))
            {
                outputText.text = "⚠️ API Key Format Warning: Your API key doesn't start with 'sk-' which is the standard format for OpenAI API keys.\n\nPlease verify you're using the correct key.";
                return;
            }

            outputText.text = $"API Key Format: Appears valid (starts with {apiKey.Substring(0, Math.Min(5, apiKey.Length))}...)\n\nLength: {apiKey.Length} characters\n\nUse the /test command to verify that the key works with the API.";
        }

        // Fix the SendCommandToOpenAI method to ensure all code paths return a value
        private async Task<string> SendCommandToOpenAI(string userInput)
        {
            try
            {
                // Check API key first with more detailed error
                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new System.Exception("API key is empty. Please set your OpenAI API key using the /apikey command.");
                }

                if (apiKey == "your-api-key-here")
                {
                    throw new System.Exception("API key is set to the default placeholder. Please set your actual OpenAI API key using the /apikey command.");
                }

                using (HttpClient client = new HttpClient())
                {
                    // Add API key as authorization header
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    // Add user message to conversation history
                    conversationHistory.Add(new Message { role = "user", content = userInput });

                    // Trim conversation history if needed
                    TrimConversationHistory();

                    // Create request content
                    var request = new ChatGPTRequest
                    {
                        model = modelName,
                        messages = conversationHistory,
                        max_tokens = maxTokens
                    };

                    string jsonRequest = JsonConvert.SerializeObject(request);
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                    // Send request with timeout
                    client.Timeout = System.TimeSpan.FromSeconds(apiTimeoutDuration);
                    var response = await client.PostAsync(API_URL, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new System.Exception($"{(int)response.StatusCode} ({response.ReasonPhrase}) - {errorContent}");
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // Parse response
                    var gptResponse = JsonConvert.DeserializeObject<ChatGPTResponse>(jsonResponse);
                    string responseMessage = gptResponse.choices[0].message.content;

                    // Log token usage
                    if (gptResponse.usage != null)
                    {
                        Debug.Log($"API Usage - Prompt tokens: {gptResponse.usage.prompt_tokens}, " +
                                  $"Completion tokens: {gptResponse.usage.completion_tokens}, " +
                                  $"Total tokens: {gptResponse.usage.total_tokens}");
                    }

                    // Add assistant response to conversation history
                    conversationHistory.Add(new Message { role = "assistant", content = responseMessage });

                    return responseMessage;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error sending command to OpenAI API: {ex.Message}");
                throw; // Re-throw the exception to be handled by the caller
            }
        }
        public void SetApiKey(string newApiKey)
        {
            // Trim any whitespace that might have been accidentally included
            newApiKey = newApiKey.Trim();

            // Check for common issues
            if (string.IsNullOrEmpty(newApiKey))
            {
                outputText.text = "Error: API key cannot be empty";
                return;
            }

            // Store the key
            apiKey = newApiKey;
            SaveConfig();

            // Provide helpful feedback based on key format
            if (!apiKey.StartsWith("sk-"))
            {
                outputText.text = "API Key set, but it doesn't start with 'sk-'. Please verify you're using the correct OpenAI API key format.\n\nUse /verify to check the key format.";
            }
            else
            {
                string maskedKey = apiKey.Substring(0, 5) + "..." + apiKey.Substring(apiKey.Length - 4);
                outputText.text = $"API Key successfully set to: {maskedKey}\n\nUse /test to verify it works with the API.";
            }

            // If we were in offline mode, try to go back online
            if (offlineMode)
            {
                offlineMode = false;
                // Update button
                Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
                if (offlineButton != null)
                {
                    Image buttonImage = offlineButton.GetComponent<Image>();
                    Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                    if (buttonImage != null && buttonText != null)
                    {
                        buttonImage.color = new Color(0.4f, 0.4f, 0.8f); // Online color (blue)
                        buttonText.text = "Online";
                    }
                }
                UpdateStatus("Switched back to Online Mode with new API key");
            }
        }
        private void DisplayApiKeyStatus()
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey == "your-api-key-here")
            {
                outputText.text = "API Key is not configured. Please use /apikey command to set your OpenAI API key.";
            }
            else
            {
                // Show masked key for security
                string maskedKey = apiKey.Substring(0, 3) + "..." + apiKey.Substring(apiKey.Length - 4);
                outputText.text = $"API Key is configured (key starts with {maskedKey}). Use /apikey command if you need to update it.";
            }
        }



        private void TrySpawnItemBasedOnCommand(string command)
        {
            string lowerCommand = command.ToLower();

            // Attempt to determine the intent
            bool isSummonIntent = lowerCommand.Contains("summon") ||
                                 lowerCommand.Contains("spawn") ||
                                 lowerCommand.Contains("create") ||
                                 lowerCommand.Contains("give me");

            if (isSummonIntent)
            {
                // Process weapon summons
                if (lowerCommand.Contains("sword"))
                {
                    TrySpawnItemBasedOnName("Sword");
                }
                else if (lowerCommand.Contains("axe"))
                {
                    TrySpawnItemBasedOnName("Axe");
                }
                else if (lowerCommand.Contains("bow"))
                {
                    TrySpawnItemBasedOnName("Bow");
                }
                else if (lowerCommand.Contains("staff") || lowerCommand.Contains("wand"))
                {
                    TrySpawnItemBasedOnName("Staff");
                }
                else if (lowerCommand.Contains("fire") || lowerCommand.Contains("flame"))
                {
                    TrySpawnItemBasedOnName("Fire");
                }
                else if (lowerCommand.Contains("lightning") || lowerCommand.Contains("thunder"))
                {
                    TrySpawnItemBasedOnName("Lightning");
                }
                else if (lowerCommand.Contains("gravity"))
                {
                    TrySpawnItemBasedOnName("Gravity");
                }
                else if (lowerCommand.Contains("ice") || lowerCommand.Contains("frost"))
                {
                    TrySpawnItemBasedOnName("Ice");
                }
            }
        }

        private void TrySpawnItemBasedOnName(string itemTypeName)
        {
            try
            {
                Debug.Log($"ChatGPT Spell Controller: Attempting to spawn item of type: {itemTypeName}");
                Debug.Log($"ChatGPT Spell Controller: Item spawning simulation for: {itemTypeName} - Would spawn here if implemented");

                // In a full implementation, you would add code like:
                /*
                string catalogID = FindCatalogIDByName(itemTypeName);
                if (!string.IsNullOrEmpty(catalogID))
                {
                    // Get spawn position in front of player
                    Vector3 spawnPosition = Player.local.head.transform.position + 
                                           Player.local.head.transform.forward * 1.0f;
                                           
                    // Spawn the item using the game's API
                    ItemData itemData = Catalog.GetData<ItemData>(catalogID);
                    Item spawnedItem = ItemManager.local.SpawnItem(itemData, spawnPosition, 
                                      Player.local.head.transform.rotation);
                                      
                    // Additional setup for the spawned item
                    if (spawnedItem != null)
                    {
                        // If it's a spell, configure the spell parameters
                        if (spawnedItem.TryGetComponent<SpellCaster>(out var spellCaster))
                        {
                            spellCaster.SetIntensity(1.0f);
                        }
                    }
                }
                */
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ChatGPT Spell Controller: Error spawning item - {ex.Message}");
            }
        }

        private void ToggleUI()
        {
            isPanelVisible = !isPanelVisible;

            if (commandPanel != null)
            {
                commandPanel.SetActive(isPanelVisible);
            }
        }

        private void ToggleOfflineMode()
        {
            offlineMode = !offlineMode;
            Debug.Log($"Toggle offline mode: Now {(offlineMode ? "OFFLINE" : "ONLINE")}");

            // Update button appearance
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Image buttonImage = offlineButton.GetComponent<Image>();
                Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    // Update button color
                    buttonImage.color = offlineMode ? new Color(0.8f, 0.4f, 0.4f) : new Color(0.4f, 0.4f, 0.8f);

                    // Update button text - show the current mode
                    buttonText.text = offlineMode ? "Offline" : "Online";
                    Debug.Log($"Updated button text to: {buttonText.text}");
                }
            }

            UpdateStatus(offlineMode ? "Switched to Offline Mode" : "Switched to Online Mode");
        }

        private void LogModeState(string location)
        {
            Debug.Log($"[{location}] Mode state: offlineMode = {offlineMode}, Button text: {GetButtonText()}");
        }

        public void ForceOnlineMode()
        {
            // Force into online mode
            offlineMode = false;
            apiCallInProgress = false;

            // Update button
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Image buttonImage = offlineButton.GetComponent<Image>();
                Text buttonText = offlineButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    buttonImage.color = new Color(0.4f, 0.4f, 0.8f); // Online color (blue)
                    buttonText.text = "Online";
                }
            }

            UpdateStatus("Forced Online Mode");
            Debug.Log("ForceOnlineMode called, mode set to ONLINE");
        }

        private string GetButtonText()
        {
            Transform offlineButton = uiCanvas?.transform.Find("CommandPanel/OfflineButton");
            if (offlineButton != null)
            {
                Text buttonText = offlineButton.Find("Text")?.GetComponent<Text>();
                if (buttonText != null)
                {
                    return buttonText.text;
                }
            }
            return "unknown";
        }
        private void ToggleAdvancedMode()
        {
            isAdvancedMode = !isAdvancedMode;

            // Update button appearance
            Transform advancedButton = uiCanvas?.transform.Find("CommandPanel/AdvancedButton");
            if (advancedButton != null)
            {
                Image buttonImage = advancedButton.GetComponent<Image>();
                Text buttonText = advancedButton.Find("Text").GetComponent<Text>();

                if (buttonImage != null && buttonText != null)
                {
                    buttonImage.color = isAdvancedMode ? new Color(0.6f, 0.3f, 0.7f) : new Color(0.3f, 0.3f, 0.7f);
                    buttonText.text = isAdvancedMode ? "Basic" : "Advanced";
                }
            }

            UpdateStatus(isAdvancedMode ?
                "Advanced Mode: Providing detailed game code context" :
                "Basic Mode: Standard AI responses");

            // Save config with new setting
            SaveConfig();
        }
    }
}