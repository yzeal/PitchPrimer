using UnityEngine;
using UnityEngine.UI;

// COPILOT CONTEXT: Simple test manager for refactored pitch analysis system
// Tests MicAnalysisRefactored with PitchVisualizer integration

public class RefactoredTestManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private MicAnalysisRefactored micAnalysis;
    [SerializeField] private PitchVisualizer userVisualizer;
    [SerializeField] private MicrophoneSelector micSelector;
    
    [Header("UI")]
    [SerializeField] private Button startTestButton;
    [SerializeField] private Button stopTestButton;
    [SerializeField] private Button clearVisualsButton;
    
    [Header("Test Settings")]
    [SerializeField] private VisualizationSettings testVisualizationSettings;
    
    private bool isTestRunning = false;
    
    void Start()
    {
        InitializeTest();
        SetupUI();
    }
    
    private void InitializeTest()
    {
        // Initialize User Visualizer
        if (userVisualizer != null)
        {
            userVisualizer.Initialize(testVisualizationSettings);
            Debug.Log("User Visualizer initialized");
        }
        
        // Subscribe to pitch detection events
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected += OnPitchDetected;
            Debug.Log("Subscribed to MicAnalysis events");
        }
    }
    
    private void SetupUI()
    {
        if (startTestButton != null)
            startTestButton.onClick.AddListener(StartTest);
            
        if (stopTestButton != null)
            stopTestButton.onClick.AddListener(StopTest);
            
        if (clearVisualsButton != null)
            clearVisualsButton.onClick.AddListener(ClearVisuals);
    }
    
    public void StartTest()
    {
        if (isTestRunning) return;
        
        Debug.Log("=== Starting Refactored Test ===");
        
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
            isTestRunning = true;
            Debug.Log("Test started - speak into microphone!");
        }
        else
        {
            Debug.LogError("MicAnalysis component not assigned!");
        }
    }
    
    public void StopTest()
    {
        if (!isTestRunning) return;
        
        Debug.Log("=== Stopping Refactored Test ===");
        
        if (micAnalysis != null)
        {
            micAnalysis.StopAnalysis();
            isTestRunning = false;
            Debug.Log("Test stopped");
        }
    }
    
    public void ClearVisuals()
    {
        if (userVisualizer != null)
        {
            userVisualizer.ClearAll();
            Debug.Log("Visuals cleared");
        }
    }
    
    // Event Handler für Pitch Detection
    private void OnPitchDetected(PitchDataPoint pitchData)
    {
        if (userVisualizer != null)
        {
            userVisualizer.AddRealtimePitchData(pitchData);
        }
        
        // Debug output für interessante Pitches
        if (pitchData.HasPitch)
        {
            Debug.Log($"Pitch: {pitchData.frequency:F1}Hz, Confidence: {pitchData.confidence:F2}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup event subscription
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected -= OnPitchDetected;
        }
    }
    
    // Status Info für Inspector
    [Header("Runtime Info")]
    [SerializeField] private bool showRuntimeInfo = true;
    
    void OnGUI()
    {
        if (!showRuntimeInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== Refactored Test Status ===");
        GUILayout.Label($"Test Running: {isTestRunning}");
        
        if (micAnalysis != null)
        {
            GUILayout.Label($"Analyzing: {micAnalysis.IsAnalyzing}");
            GUILayout.Label($"Calibrating: {micAnalysis.IsCalibrating}");
            GUILayout.Label($"Device: {micAnalysis.CurrentDevice}");
            GUILayout.Label($"Ambient Level: {micAnalysis.AmbientNoiseLevel:F4}");
        }
        
        GUILayout.EndArea();
    }
}