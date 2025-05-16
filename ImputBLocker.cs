using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AiSorcery
{
    class ImputBLocker
    {

        public class InputBlocker : MonoBehaviour
        {
            private bool isBlocking = false;

            // Keep track of original input settings
            private Dictionary<string, float> originalAxisValues = new Dictionary<string, float>();
            private Dictionary<KeyCode, bool> originalKeyStates = new Dictionary<KeyCode, bool>();

            // List of inputs to block
            private string[] axesToBlock = new string[] { "Horizontal", "Vertical", "Mouse X", "Mouse Y" };
            private KeyCode[] keysToBlock = new KeyCode[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.LeftShift };

            public void StartBlocking()
            {
                isBlocking = true;
            }

            public void StopBlocking()
            {
                isBlocking = false;
            }

            void Update()
            {
                if (isBlocking)
                {
                    // Block axis inputs
                    foreach (string axis in axesToBlock)
                    {
                        // This is a simplified approach and might not work for all games
                        // For more complex games, you might need to use reflection or other techniques
                    }

                    // Block key inputs
                    foreach (KeyCode key in keysToBlock)
                    {
                        // Similarly, blocking key inputs directly is challenging
                        // This is just a placeholder for the concept
                    }
                }
            }
        }
    }
}