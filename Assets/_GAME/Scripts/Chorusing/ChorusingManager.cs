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

    [Header("Native Recording Silence")]
    [Tooltip("Requested silence duration between native recording loops (seconds)")]
    [SerializeField] private float requestedSilenceDuration = 0.6f;
    [Tooltip("Actual silence duration (quantized to analysis intervals)")]
    [SerializeField] private float quantizedSilenceDuration = 0f; // Read-only, calculated

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    private List<PitchDataPoint> nativePitchData;
    private bool isChorusingActive = false;
    private float chorusingStartTime;

    // NEW: Quantified silence system
    private bool isInSilencePeriod = false;
    private float silenceStartTime = 0f;
    private float lastLoopEndTime = 0f;

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
        if (isChorusingActive)
        {
            UpdateQuantizedAudioSilenceLogic();
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

        // CRITICAL: Calculate quantized silence BEFORE starting
        CalculateQuantizedSilence();

        // Start microphone analysis
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
        }

        // Start audio without Unity auto-loop
        if (nativeAudioSource != null)
        {
            nativeAudioSource.clip = nativeClip;
            nativeAudioSource.loop = false; // Manual looping with silence
            nativeAudioSource.Play();
            
            // Track when this loop should end
            lastLoopEndTime = Time.time + nativeClip.length;
            isInSilencePeriod = false;
            
            DebugLog($"Audio started, will end at: {lastLoopEndTime:F2}s (current: {Time.time:F2}s)");
        }

        // Pre-render native visualization with quantized silence
        if (nativeVisualizer != null)
        {
            nativeVisualizer.PreRenderNativeTrack(nativePitchData, quantizedSilenceDuration);
        }

        isChorusingActive = true;
        chorusingStartTime = Time.time;

        DebugLog($"Chorusing started with quantized silence: {quantizedSilenceDuration:F3}s");
    }

    // FIXED: Calculate silence duration without touching visualizer settings
    private void CalculateQuantizedSilence()
    {
        // QUANTIZE: Round to nearest multiple of analysis interval
        int silenceIntervals = Mathf.RoundToInt(requestedSilenceDuration / analysisInterval);
        quantizedSilenceDuration = silenceIntervals * analysisInterval;
        
        DebugLog($"QUANTIZED SILENCE CALCULATION:");
        DebugLog($"  Requested: {requestedSilenceDuration:F3}s");
        DebugLog($"  Analysis interval: {analysisInterval:F3}s");
        DebugLog($"  Silence intervals: {silenceIntervals}");
        DebugLog($"  Quantized silence: {quantizedSilenceDuration:F3}s");
        DebugLog($"  Visual cubes: {silenceIntervals} silence cubes");
        
        // REMOVED: No longer needed - visualizer gets silence as parameter
        // OLD CODE: settings.silenceBetweenReps = quantizedSilenceDuration;
        
        DebugLog($"Quantized silence calculated: {quantizedSilenceDuration:F3}s");
    }

    // NEW: Public method to change silence at runtime
    public void SetSilenceDuration(float newSilenceDuration)
    {
        requestedSilenceDuration = newSilenceDuration;
        
        if (isChorusingActive)
        {
            CalculateQuantizedSilence();
            DebugLog($"Silence updated during playback to: {quantizedSilenceDuration:F3}s");
        }
    }

    // NEW: Update-based quantized silence logic
    private void UpdateQuantizedAudioSilenceLogic()
    {
        if (nativeAudioSource == null || nativeClip == null) return;

        float currentTime = Time.time;

        if (!isInSilencePeriod)
        {
            // Check if audio should have ended
            if (currentTime >= lastLoopEndTime)
            {
                // Enter QUANTIZED silence period
                isInSilencePeriod = true;
                silenceStartTime = currentTime;
                
                DebugLog($"?? Entering QUANTIZED silence period ({quantizedSilenceDuration:F3}s)");
            }
        }
        else
        {
            // Check if QUANTIZED silence period should end
            if (currentTime >= silenceStartTime + quantizedSilenceDuration)
            {
                // Exit silence, start next loop
                isInSilencePeriod = false;
                nativeAudioSource.Play();
                lastLoopEndTime = currentTime + nativeClip.length;
                
                DebugLog($"?? QUANTIZED silence ended ({quantizedSilenceDuration:F3}s), starting next audio loop");
            }
        }
    }

    public void StopChorusing()
    {
        isChorusingActive = false;
        
        // SIMPLE: Just reset flags
        isInSilencePeriod = false;

        if (micAnalysis != null)
        {
            micAnalysis.StopAnalysis();
        }

        if (nativeAudioSource != null)
        {
            nativeAudioSource.Stop();
        }

        // Clear visualizations
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

    // ENHANCED: Visualization update with quantized timing
    private void UpdateNativeVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null && nativeAudioSource != null)
        {
            float playbackTime;

            if (isInSilencePeriod)
            {
                // During QUANTIZED silence: extend beyond audio duration
                float silenceElapsed = Time.time - silenceStartTime;
                playbackTime = nativeClip.length + silenceElapsed;
            }
            else
            {
                // During audio: use actual audio time
                playbackTime = nativeAudioSource.time;
            }

            nativeVisualizer.UpdateNativeTrackPlayback(playbackTime, nativePitchData);
            
            // DEBUG: Show quantized timing every 2 seconds
            if (enableDebugLogging && Time.time % 2f < 0.1f)
            {
                float totalLoop = nativeClip.length + quantizedSilenceDuration;
                float loopPosition = playbackTime % totalLoop;
                DebugLog($"QUANTIZED TIMING: playing={nativeAudioSource.isPlaying}, silence={isInSilencePeriod}, " +
                        $"playbackTime={playbackTime:F2}s, loopPos={loopPosition:F2}s, totalLoop={totalLoop:F2}s");
            }
        }
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

    // NEW: Getter for quantized silence info
    public float GetQuantizedSilenceDuration() => quantizedSilenceDuration;
    public bool IsInSilencePeriod() => isInSilencePeriod;

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