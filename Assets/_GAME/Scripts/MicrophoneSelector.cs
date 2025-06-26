using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MicrophoneSelector : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown microphoneDropdown;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button startAnalysisButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Analysis")]
    [SerializeField] private MicAnalysis micAnalysis;

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
            "discord"
        };

        foreach (string pattern in virtualPatterns)
        {
            if (lowerName.Contains(pattern))
                return true;
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
            // Stop any existing recording
            micAnalysis.StopAnalysis();

            // Set the selected microphone
            micAnalysis.SetMicrophone(selectedMicrophone);

            // Start analysis
            micAnalysis.StartAnalysis();

            UpdateStatus($"Started analysis with: {selectedMicrophone}");

            // Hide UI after starting
            gameObject.SetActive(false);
        }
        else
        {
            UpdateStatus("MicAnalysis component not found!");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"MicrophoneSelector: {message}");
    }

    // Public method to show selector again
    public void ShowSelector()
    {
        gameObject.SetActive(true);
        RefreshMicrophoneList();
    }
}