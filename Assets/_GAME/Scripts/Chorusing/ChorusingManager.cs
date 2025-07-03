using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Main controller for chorusing exercises
// Coordinates native recording playback with user microphone input
// Manages dual-track visualization and synchronization
// UPDATED: References visualizers directly to avoid duplicate settings

public class ChorusingManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private MicAnalysisRefactored micAnalysis;
    [SerializeField] private PitchVisualizer userVisualizer;
    [SerializeField] private PitchVisualizer nativeVisualizer;
    [SerializeField] private AudioSource nativeAudioSource;

    [Header("Analysis Settings")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;

    [Header("Native Recording")]
    [SerializeField] private AudioClip nativeClip;
    [SerializeField] private float analysisInterval = 0.1f;
    [SerializeField] private bool autoLoop = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    private List<PitchDataPoint> nativePitchData;
    private bool isChorusingActive = false;
    private float chorusingStartTime;

    void Start()
    {
        InitializeComponents();
        if (nativeClip != null)
        {
            PreAnalyzeNativeRecording();
        }
    }

    void Update()
    {
        if (isChorusingActive && nativeAudioSource != null && nativeAudioSource.isPlaying)
        {
            UpdateNativeVisualization();
        }
    }

    public void StartChorusing()
    {
        if (nativePitchData == null || nativePitchData.Count == 0)
        {
            Debug.LogError("No native recording data available!");
            return;
        }

        // Starte Mikrofon-Analyse
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
        }

        // Starte native Aufnahme
        if (nativeAudioSource != null)
        {
            nativeAudioSource.clip = nativeClip;
            nativeAudioSource.loop = autoLoop;
            nativeAudioSource.Play();
        }

        // Pre-rendere native Visualisierung (uses its own settings)
        if (nativeVisualizer != null)
        {
            nativeVisualizer.PreRenderNativeTrack(nativePitchData);
        }

        isChorusingActive = true;
        chorusingStartTime = Time.time;

        DebugLog("Chorusing started!");
    }

    public void StopChorusing()
    {
        isChorusingActive = false;

        if (micAnalysis != null)
        {
            micAnalysis.StopAnalysis();
        }

        if (nativeAudioSource != null)
        {
            nativeAudioSource.Stop();
        }

        // Clear visualizations (each uses its own settings)
        if (userVisualizer != null)
        {
            userVisualizer.ClearAll();
        }

        if (nativeVisualizer != null)
        {
            nativeVisualizer.ClearAll();
        }

        DebugLog("Chorusing stopped!");
    }

    private void InitializeComponents()
    {
        // Setup Event-Integration mit MicAnalysisRefactored
        if (micAnalysis != null)
        {
            // Subscribe to pitch detection events
            micAnalysis.OnPitchDetected += OnUserPitchDetected;
            DebugLog("Subscribed to MicAnalysisRefactored events");
        }
        else
        {
            Debug.LogError("MicAnalysisRefactored component not assigned!");
        }

        // REMOVED: Manual initialization - visualizers handle their own settings!
        // The PitchVisualizers will use their own VisualizationSettings from their Inspector

        // Setup AudioSource
        if (nativeAudioSource == null)
        {
            nativeAudioSource = gameObject.AddComponent<AudioSource>();
        }
        nativeAudioSource.playOnAwake = false;
        
        DebugLog("ChorusingManager components initialized");
    }

    private void PreAnalyzeNativeRecording()
    {
        if (nativeClip == null) 
        {
            DebugLog("No native clip assigned for pre-analysis");
            return;
        }

        DebugLog($"Pre-analyzing native recording: {nativeClip.name}");

        // Use shared PitchAnalyzer with our analysis settings
        nativePitchData = PitchAnalyzer.PreAnalyzeAudioClip(nativeClip, analysisSettings, analysisInterval);

        // Optional: Smoothing
        if (analysisSettings.useSmoothing)
        {
            nativePitchData = PitchAnalyzer.SmoothPitchData(nativePitchData, analysisSettings.historySize);
        }

        DebugLog($"Native recording analyzed: {nativePitchData.Count} data points");

        // Debug: Show statistics
        if (enableDebugLogging)
        {
            var stats = PitchAnalyzer.CalculateStatistics(nativePitchData);
            DebugLog($"Native recording stats: {stats}");
        }
    }

    private void UpdateNativeVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null && nativeAudioSource != null)
        {
            float playbackTime = nativeAudioSource.time;
            nativeVisualizer.UpdateNativeTrackPlayback(playbackTime, nativePitchData);
        }
    }

    // Event Handler für MicAnalysisRefactored
    private void OnUserPitchDetected(PitchDataPoint pitchData)
    {
        if (isChorusingActive && userVisualizer != null)
        {
            // User visualizer uses its own settings for display
            userVisualizer.AddRealtimePitchData(pitchData);
            
            // Debug output für interessante Events
            if (enableDebugLogging && pitchData.HasPitch)
            {
                DebugLog($"User pitch: {pitchData.frequency:F1}Hz at {pitchData.timestamp:F2}s");
            }
        }
    }

    // Public Methods für UI
    public void SetNativeRecording(AudioClip clip)
    {
        nativeClip = clip;
        PreAnalyzeNativeRecording();
    }

    // ADDED: Helper methods to access visualizer settings (read-only)
    public VisualizationSettings GetUserVisualizationSettings()
    {
        return userVisualizer != null ? userVisualizer.GetSettings() : null;
    }

    public VisualizationSettings GetNativeVisualizationSettings()
    {
        return nativeVisualizer != null ? nativeVisualizer.GetSettings() : null;
    }

    // Public getters
    public bool IsChorusingActive => isChorusingActive;
    public float GetNativePlaybackTime() => nativeAudioSource != null ? nativeAudioSource.time : 0f;
    public List<PitchDataPoint> GetNativePitchData() => nativePitchData;

    // Debug Methods
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[ChorusingManager] {message}");
        }
    }

    void OnDestroy()
    {
        // WICHTIG: Event-Subscription cleanup
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected -= OnUserPitchDetected;
        }
    }
}