using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace ChatGPTMod
{
    public class ChatGPTModInitializer : LevelModule
    {
        private static bool hasInitialized = false;

        public override IEnumerator OnLoadCoroutine()
        {
            Debug.Log($"ChatGPT Mod Level Module: Loading in {level.data.id}");

            // Wait for level to fully initialize
            yield return new WaitForSeconds(3.0f);

            // Only initialize if enabled in mod options
            if (ChatGPTModOptions.EnableChatGPTController)
            {
                GameObject existingController = GameObject.Find("ChatGPTSpellController");

                if (existingController == null && !hasInitialized)
                {
                    Debug.Log("ChatGPT Spell Controller: Creating new controller from LevelModule");
                    GameObject controllerObject = new GameObject("ChatGPTSpellController");
                    controllerObject.AddComponent<ChatGPTSpellController>();
                    GameObject.DontDestroyOnLoad(controllerObject);
                    Debug.Log("ChatGPT Spell Controller created successfully from LevelModule!");
                    hasInitialized = true;
                }
                else if (existingController != null)
                {
                    Debug.Log("ChatGPT Spell Controller: Found existing controller, refreshing position");
                    ChatGPTSpellController controller = existingController.GetComponent<ChatGPTSpellController>();
                    if (controller != null)
                    {
                        controller.RefreshPanelPosition();
                    }
                }
            }
            else
            {
                Debug.Log("ChatGPT Mod: Controller disabled in mod options");
            }
        }
    }
}