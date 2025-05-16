using ThunderRoad;
using UnityEngine;

namespace ChatGPTMod
{
    public class ChatGPTModOptions
    {
        // Static field to track if the mod is enabled
        [ModOption(name: "Enable ChatGPT", tooltip: "Enables Chatgpt Ui", saveValue = true, order = 2)]
        public static bool EnableChatGPTController = true;

        // Static field for API key configuration
        [ModOption(name: "OpenAI API Key", tooltip = "Enter your OpenAI API key here (leave empty to use default)", saveValue = true, order = 1)]
        public static string ApiKey = ""; // Empty by default

        // Optional: Add other configuration options
        [ModOption(name: "Auto-Show UI", tooltip: "Automatically show the UI panel when loading a level", saveValue = true, order = 2)]
        public static bool AutoShowUI = true;

        [ModOption(name: "UI Scale", tooltip: "Adjust the size of the UI panel", saveValue = true, order = 3, valueSourceName = nameof(ScaleOptions))]
        public static float UIScale = 1.0f;

        [ModOption(name: "Debug Mode", tooltip: "Enable detailed error messages", saveValue = true, order = 4)]
        public static bool DebugMode = false;

        [ModOption(name: "Offline Mode", tooltip: "Process commands without using OpenAI API", saveValue = true, order = 5)]
        public static bool OfflineMode = false;

        public static ModOptionFloat[] ScaleOptions = new[]
        {
            new ModOptionFloat("0.5x (Small)", 0.5f),
            new ModOptionFloat("0.75x", 0.75f),
            new ModOptionFloat("1.0x (Default)", 1.0f),
            new ModOptionFloat("1.25x", 1.25f),
            new ModOptionFloat("1.5x (Large)", 1.5f)
        };
    }
}