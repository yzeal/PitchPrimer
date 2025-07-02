using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// COPILOT CONTEXT: Updated microphone selector for refactored system
// Now uses MicAnalysisRefactored instead of legacy MicAnalysis
// Maintains same UI functionality with improved backend integration

public class MicrophoneSelector : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown microphoneDropdown;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button startAnalysisButton;
    [SerializeField] private Button debugButton; // Optional debug button
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Analysis")]
    [SerializeField] private MicAnalysisRefactored micAnalysis; // CHANGED: MicAnalysisRefactored

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    private List<string> availableMicrophones;
    private string selectedMicrophone;

    void Start()
    {
        InitializeUI();
        RefreshMicrophoneList();
    }

    private void InitializeUI()
    {
        // Setup button events
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshMicrophoneList);

        if (startAnalysisButton != null)
            startAnalysisButton.onClick.AddListener(StartMicrophoneAnalysis);

        if (debugButton != null)
            debugButton.onClick.AddListener(ShowDebugInfo);

        // Setup dropdown event
        if (microphoneDropdown != null)
            microphoneDropdown.onValueChanged.AddListener(OnMicrophoneSelected);

        // Initially disable start button
        if (startAnalysisButton != null)
            startAnalysisButton.interactable = false;
    }

    public void RefreshMicrophoneList()
    {
        availableMicrophones = new List<string>();

        // Get all microphone devices
        foreach (string device in Microphone.devices)
        {
            // Filter out virtual audio devices
            if (!IsVirtualAudioDevice(device))
            {
                availableMicrophones.Add(device);
            }
        }

        UpdateDropdown();
        UpdateStatus($"Found {availableMicrophones.Count} microphone(s)");
        DebugLog($"Refreshed microphone list: {availableMicrophones.Count} devices found");
    }

    private bool IsVirtualAudioDevice(string deviceName)
    {
        string lowerName = deviceName.ToLower();

        // Common virtual audio device patterns
        string[] virtualPatterns = {
            "virtual",
            "oculus",
            "vr", 
            "steamvr",
            "valve",
            "cable",
            "voicemeeter",
            "obs",
            "discord",
            "blackhole" // macOS virtual audio
        };

        foreach (string pattern in virtualPatterns)
        {
            if (lowerName.Contains(pattern))
            {
                DebugLog($"Filtered out virtual device: {deviceName}");
                return true;
            }
        }

        return false;
    }

    private void UpdateDropdown()
    {
        if (microphoneDropdown == null) return;

        microphoneDropdown.ClearOptions();

        if (availableMicrophones.Count == 0)
        {
            microphoneDropdown.AddOptions(new List<string> { "No microphones found" });
            microphoneDropdown.interactable = false;
            if (startAnalysisButton != null)
                startAnalysisButton.interactable = false;
        }
        else
        {
            microphoneDropdown.AddOptions(availableMicrophones);
            microphoneDropdown.interactable = true;
            microphoneDropdown.value = 0;
            selectedMicrophone = availableMicrophones[0];

            if (startAnalysisButton != null)
                startAnalysisButton.interactable = true;
        }
    }

    public void OnMicrophoneSelected(int index)
    {
        if (index >= 0 && index < availableMicrophones.Count)
        {
            selectedMicrophone = availableMicrophones[index];
            UpdateStatus($"Selected: {selectedMicrophone}");
            DebugLog($"Microphone selected: {selectedMicrophone}");
        }
    }

    public void StartMicrophoneAnalysis()
    {
        if (string.IsNullOrEmpty(selectedMicrophone))
        {
            UpdateStatus("Please select a microphone first!");
            return;
        }

        if (micAnalysis != null)
        {
            DebugLog($"Starting analysis with microphone: {selectedMicrophone}");

            // Stop any existing recording
            micAnalysis.StopAnalysis();

            // Set the selected microphone
            micAnalysis.SetMicrophone(selectedMicrophone);

            // Start analysis
            micAnalysis.StartAnalysis();

            UpdateStatus($"Started analysis with: {selectedMicrophone}");
            DebugLog("Analysis started successfully");

            // Optional: Hide UI after starting (comment out if you want to keep it visible)
            // gameObject.SetActive(false);
        }
        else
        {
            UpdateStatus("MicAnalysisRefactored component not found!");
            Debug.LogError("MicAnalysisRefactored component is not assigned in MicrophoneSelector!");
        }
    }

    // NEW: Debug info method
    public void ShowDebugInfo()
    {
        if (micAnalysis != null)
        {
            Debug.Log($"=== MICROPHONE SELECTOR DEBUG ===");
            Debug.Log($"Selected Device: {selectedMicrophone}");
            Debug.Log($"Is Analyzing: {micAnalysis.IsAnalyzing}");
            Debug.Log($"Is Calibrating: {micAnalysis.IsCalibrating}");
            Debug.Log($"Current Device: {micAnalysis.CurrentDevice}");
            Debug.Log($"Ambient Noise Level: {micAnalysis.AmbientNoiseLevel:F4}");
            
            // Show all available microphones
            Debug.Log($"Available Microphones:");
            for (int i = 0; i < availableMicrophones.Count; i++)
            {
                Debug.Log($"  {i}: {availableMicrophones[i]}");
            }
        }
        else
        {
            Debug.Log("MicAnalysisRefactored component is null!");
        }
    }

    // NEW: Stop analysis method for UI
    public void StopMicrophoneAnalysis()
    {
        if (micAnalysis != null)
        {
            micAnalysis.StopAnalysis();
            UpdateStatus("Analysis stopped");
            DebugLog("Analysis stopped by user");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"MicrophoneSelector: {message}");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[MicrophoneSelector] {message}");
        }
    }

    // Public method to show selector again
    public void ShowSelector()
    {
        gameObject.SetActive(true);
        RefreshMicrophoneList();
        DebugLog("Selector shown");
    }

    // NEW: Runtime status display
    [Header("Runtime Info")]
    [SerializeField] private bool showRuntimeInfo = false;

    void OnGUI()
    {
        if (!showRuntimeInfo || micAnalysis == null) return;

        GUILayout.BeginArea(new Rect(10, 150, 300, 150));
        GUILayout.Label("=== Microphone Selector Status ===");
        GUILayout.Label($"Selected: {selectedMicrophone}");
        GUILayout.Label($"Analyzing: {micAnalysis.IsAnalyzing}");
        GUILayout.Label($"Calibrating: {micAnalysis.IsCalibrating}");
        GUILayout.Label($"Device: {micAnalysis.CurrentDevice}");
        GUILayout.Label($"Ambient: {micAnalysis.AmbientNoiseLevel:F4}");
        GUILayout.EndArea();
    }
}