using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class SelfModifyingAITester : MonoBehaviour
{
    public SelfModifyingAI codeModifier;
    public InputField inputField;
    public Text outputText;
    public Button testButton;

    void Start()
    {
        // Find components if not set in inspector
        if (codeModifier == null)
            codeModifier = FindObjectOfType<SelfModifyingAI>();

        if (testButton != null)
            testButton.onClick.AddListener(RunTest);
    }

    public void RunTest()
    {
        if (inputField != null && !string.IsNullOrEmpty(inputField.text))
        {
            Debug.Log("Running self-modification test with input: " + inputField.text);
            _ = TestModification(inputField.text);
        }
        else
        {
            Debug.Log("Running self-modification with hardcoded test");
            _ = TestModification("Make enemies run away when their health is low");
        }
    }

    private async Task TestModification(string request)
    {
        if (codeModifier == null)
        {
            Debug.LogError("SelfModifyingAI component not found!");
            if (outputText != null)
                outputText.text = "ERROR: SelfModifyingAI component not found!";
            return;
        }

        try
        {
            // Call the code modification process directly
            string result = await codeModifier.ProcessCodeModificationRequest(request);

            Debug.Log("Code modification result: " + result);
            if (outputText != null)
                outputText.text = result;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during code modification: {ex.Message}\n{ex.StackTrace}");
            if (outputText != null)
                outputText.text = $"Error: {ex.Message}";
        }
    }
}