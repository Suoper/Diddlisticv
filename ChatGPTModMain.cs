using ThunderRoad;
using UnityEngine;
using System.Collections;

namespace ChatGPTMod
{
    public class ChatGPTModMain : ThunderScript
    {
        private static bool hasInitialized = false;
        private GameObject helperObject;

        public override void ScriptEnable()
        {
            Debug.Log("ChatGPT Mod: Main script enabled");

            // Create a helper GameObject with a MonoBehaviour to handle coroutines and events
            helperObject = new GameObject("ChatGPTModHelper");
            GameObject.DontDestroyOnLoad(helperObject);
            ChatGPTModHelper helper = helperObject.AddComponent<ChatGPTModHelper>();

            // Initial setup
            InitializeController();
        }

        private void InitializeController()
        {
            // Check if controller already exists
            GameObject existingController = GameObject.Find("ChatGPTSpellController");
            if (existingController == null && !hasInitialized)
            {
                Debug.Log("ChatGPT Spell Controller: Creating new controller from ThunderScript");
                GameObject controllerObject = new GameObject("ChatGPTSpellController");
                controllerObject.AddComponent<ChatGPTSpellController>();
                GameObject.DontDestroyOnLoad(controllerObject);
                Debug.Log("ChatGPT Spell Controller created successfully!");
                hasInitialized = true;
            }
            else
            {
                Debug.Log("ChatGPT Spell Controller: Controller already exists");
            }
        }

        public override void ScriptDisable()
        {
            Debug.Log("ChatGPT Mod: Main script disabled");
            if (helperObject != null)
            {
                GameObject.Destroy(helperObject);
            }
        }
    }

    // Helper MonoBehaviour to handle coroutines and events
    public class ChatGPTModHelper : MonoBehaviour
    {
        void Awake()
        {
            // Subscribe to level load events
            // Using direct Unity events instead of ThunderRoad events
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            // Clean up event handler
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // Unity's scene loading event
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Debug.Log($"ChatGPT Mod: Scene loaded - {scene.name}");
            StartCoroutine(DelayedControllerCheck());
        }

        private IEnumerator DelayedControllerCheck()
        {
            yield return new WaitForSeconds(2.0f);

            GameObject controllerObj = GameObject.Find("ChatGPTSpellController");
            if (controllerObj == null)
            {
                Debug.Log("ChatGPT Spell Controller: Creating from scene loaded event");
                GameObject newController = new GameObject("ChatGPTSpellController");
                newController.AddComponent<ChatGPTSpellController>();
                GameObject.DontDestroyOnLoad(newController);
            }
            else
            {
                Debug.Log("ChatGPT Spell Controller: Found existing controller, resetting UI");
                ChatGPTSpellController controller = controllerObj.GetComponent<ChatGPTSpellController>();
                if (controller != null)
                {
                    // Reset the UI completely rather than just refreshing position
                    controller.ResetUI();
                }
            }
        }
    }
}