using UnityEngine;
using System.Collections.Generic;

// COPILOT CONTEXT: Main controller for chorusing exercises
// Coordinates native recording playback with user microphone input
// Manages dual-track visualization and synchronization
// UPDATED: Uses MicAnalysisRefactored with event-based integration

public class ChorusingManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private MicAnalysisRefactored micAnalysis; // CHANGED: MicAnalysisRefactored
    [SerializeField] private PitchVisualizer userVisualizer;
    [SerializeField] private PitchVisualizer nativeVisualizer;
    [SerializeField] private AudioSource nativeAudioSource;

    [Header("Settings")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private VisualizationSettings userVisualizationSettings;
    [SerializeField] private VisualizationSettings nativeVisualizationSettings;

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
        if (isChorusingActive && nativeAudioSource.isPlaying)
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
        nativeAudioSource.clip = nativeClip;
        nativeAudioSource.loop = autoLoop;
        nativeAudioSource.Play();

        // Pre-rendere native Visualisierung
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

        // Setup User Visualizer
        if (userVisualizer != null)
        {
            userVisualizer.Initialize(userVisualizationSettings);
            DebugLog("User visualizer initialized");
        }

        // Native Visualizer mit Offset (zweite Reihe)
        if (nativeVisualizer != null)
        {
            nativeVisualizationSettings.trackOffset = new Vector3(0, 0, 2f); // Dahinter
            nativeVisualizer.Initialize(nativeVisualizationSettings);
            DebugLog("Native visualizer initialized with offset");
        }

        // Setup AudioSource
        if (nativeAudioSource == null)
        {
            nativeAudioSource = gameObject.AddComponent<AudioSource>();
        }
        nativeAudioSource.playOnAwake = false;
    }

    private void PreAnalyzeNativeRecording()
    {
        if (nativeClip == null) 
        {
            DebugLog("No native clip assigned for pre-analysis");
            return;
        }

        DebugLog($"Pre-analyzing native recording: {nativeClip.name}");

        // Verwende den geteilten PitchAnalyzer
        nativePitchData = PitchAnalyzer.PreAnalyzeAudioClip(nativeClip, analysisSettings, analysisInterval);

        // Optional: Smoothing
        if (analysisSettings.useSmoothing)
        {
            nativePitchData = PitchAnalyzer.SmoothPitchData(nativePitchData, analysisSettings.historySize);
        }

        DebugLog($"Native recording analyzed: {nativePitchData.Count} data points");

        // Debug: Zeige Statistiken
        if (enableDebugLogging)
        {
            var stats = PitchAnalyzer.CalculateStatistics(nativePitchData);
            DebugLog($"Native recording stats: {stats}");
        }
    }

    private void UpdateNativeVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null)
        {
            float playbackTime = nativeAudioSource.time;
            nativeVisualizer.UpdateNativeTrackPlayback(playbackTime, nativePitchData);
        }
    }

    // UPDATED: Event Handler für MicAnalysisRefactored
    private void OnUserPitchDetected(PitchDataPoint pitchData)
    {
        if (isChorusingActive && userVisualizer != null)
        {
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