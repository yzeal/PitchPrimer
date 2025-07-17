using UnityEngine;
using System.Collections.Generic;
using System.Linq; // NEW: Für LINQ-Operationen

// COPILOT CONTEXT: Main controller for chorusing exercises
// Coordinates native recording playback with user microphone input
// Manages dual-track visualization and synchronization
// UPDATED: InitialAudioDelay system - Audio starts immediately, visual delays

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

    [Header("Audio Timing")]
    [Tooltip("Delay before starting cube movement to compensate for Unity audio start delay")]
    [SerializeField] private float initialAudioDelay = 0.5f;

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
    private float audioStartTime;

    // Audio control flags
    private bool hasAudioStarted = false;
    private bool hasDelayedStart = false;

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
            UpdateSimpleVisualization();
        }
    }

    public void StartChorusing()
    {
        if (nativePitchData == null || nativePitchData.Count == 0)
        {
            Debug.LogError("No native recording data available!");
            return;
        }

        // Calculate quantized silence (keep this)
        CalculateQuantizedSilence();

        // Start microphone analysis
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
        }

        // Setup visualization (no longer subscribes to initial audio events)
        if (nativeVisualizer != null)
        {
            nativeVisualizer.PreRenderNativeTrack(nativePitchData, quantizedSilenceDuration);
            nativeVisualizer.ResetAudioTriggers();
            
            // Subscribe to LOOP events only (not initial)
            nativeVisualizer.OnAudioLoopTrigger += TriggerAudioLoop;
        }

        // Setup audio source and START IMMEDIATELY
        if (nativeAudioSource != null)
        {
            nativeAudioSource.clip = nativeClip;
            nativeAudioSource.loop = false;
            nativeAudioSource.Play(); // Start audio immediately
            audioStartTime = Time.time;
            hasAudioStarted = true;
            DebugLog("?? Audio started immediately - visual delay active");
        }

        isChorusingActive = true;
        chorusingStartTime = Time.time;
        hasDelayedStart = false; // Will be set to true after delay

        DebugLog($"Chorusing started - visual delay: {initialAudioDelay}s");
    }

    private void TriggerAudioLoop()
    {
        if (hasAudioStarted && nativeAudioSource != null)
        {
            nativeAudioSource.Play();
            DebugLog("?? Audio loop triggered by visualizer!");
        }
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

        // Unsubscribe from LOOP events only (no initial event anymore)
        if (nativeVisualizer != null)
        {
            nativeVisualizer.OnAudioLoopTrigger -= TriggerAudioLoop;
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

        hasAudioStarted = false;
        hasDelayedStart = false;
        DebugLog("Chorusing stopped!");
    }

    private void UpdateSimpleVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null)
        {
            // Check if we should start visual updates (after delay)
            if (!hasDelayedStart)
            {
                if (Time.time - chorusingStartTime >= initialAudioDelay)
                {
                    hasDelayedStart = true;
                    DebugLog("?? Visual movement started after delay");
                }
                else
                {
                    // Still in delay period - don't update visuals
                    return;
                }
            }

            // Calculate elapsed time since visual start (not audio start)
            float visualElapsedTime = Time.time - (chorusingStartTime + initialAudioDelay);
            nativeVisualizer.UpdateNativeTrackPlayback(visualElapsedTime, nativePitchData);
        }
    }

    // KEPT: Quantized silence calculation (still needed for visual sync)
    private void CalculateQuantizedSilence()
    {
        if (nativeClip == null || nativePitchData == null)
        {
            DebugLog("No native clip or pitch data available for quantization calculation");
            quantizedSilenceDuration = 0f;
            return;
        }

        int actualAudioCubes = nativePitchData.Count;
        int requestedSilenceCubes = Mathf.RoundToInt(requestedSilenceDuration / analysisInterval);
        
        if (requestedSilenceCubes == 0)
        {
            requestedSilenceCubes = 1;
            DebugLog("ANTI-DRIFT: Minimum 1 silence cube enforced");
        }
        
        int totalCubes = actualAudioCubes + requestedSilenceCubes;
        float visualAudioTime = actualAudioCubes * analysisInterval;
        float visualTotalTime = totalCubes * analysisInterval;
        float actualSilenceNeeded = visualTotalTime - nativeClip.length;
        quantizedSilenceDuration = actualSilenceNeeded;
        
        if (quantizedSilenceDuration < 0f)
        {
            quantizedSilenceDuration += analysisInterval;
            totalCubes += 1;
            visualTotalTime = totalCubes * analysisInterval;
        }
        
        DebugLog($"QUANTIZATION: Audio={actualAudioCubes} cubes, Silence={quantizedSilenceDuration:F3}s, Total={visualTotalTime:F3}s");
    }

    public void SetSilenceDuration(float newSilenceDuration)
    {
        requestedSilenceDuration = newSilenceDuration;
        
        if (isChorusingActive)
        {
            CalculateQuantizedSilence();
            DebugLog($"Silence updated during playback to: {quantizedSilenceDuration:F3}s");
        }
    }

    public void SetInitialAudioDelay(float newDelay)
    {
        initialAudioDelay = newDelay;
        DebugLog($"Initial audio delay set to: {initialAudioDelay:F3}s");
    }

    private void InitializeComponents()
    {
        // Setup Event-Integration mit MicAnalysisRefactored
        if (micAnalysis != null)
        {
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

        nativePitchData = PitchAnalyzer.PreAnalyzeAudioClip(nativeClip, analysisSettings, analysisInterval);

        if (analysisSettings.useSmoothing)
        {
            nativePitchData = PitchAnalyzer.SmoothPitchData(nativePitchData, analysisSettings.historySize);
        }

        DebugLog($"Native recording analyzed: {nativePitchData.Count} data points");

        // NEW: Debug logging für Pitch-Range bei verschiedenen Confidence-Thresholds
        if (enableDebugLogging)
        {
            LogPitchRangeByConfidence();
            
            var stats = PitchAnalyzer.CalculateStatistics(nativePitchData);
            DebugLog($"Native recording stats: {stats}");
        }
    }

    // NEW: Debug logging für Pitch-Range bei verschiedenen Confidence-Thresholds
    private void LogPitchRangeByConfidence()
    {
        if (nativePitchData == null || nativePitchData.Count == 0)
            return;
        
        DebugLog("=== PITCH RANGE ANALYSIS BY CONFIDENCE THRESHOLDS ===");
        
        // Test verschiedene Confidence-Thresholds
        float[] thresholds = { 0.0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f };
        
        foreach (float threshold in thresholds)
        {
            var validPitches = nativePitchData
                .Where(p => p.HasPitch && p.confidence >= threshold)
                .Select(p => p.frequency)
                .ToList();
            
            if (validPitches.Count > 0)
            {
                float minPitch = validPitches.Min();
                float maxPitch = validPitches.Max();
                float avgPitch = validPitches.Average();
                float range = maxPitch - minPitch;
                
                // FIXED: Use Count property instead of Count() method
                int totalValidPitches = nativePitchData.Where(p => p.HasPitch).Count();
                float coverage = (float)validPitches.Count / totalValidPitches;
                
                DebugLog($"  Confidence >= {threshold:F1}: " +
                         $"Min={minPitch:F1}Hz, Max={maxPitch:F1}Hz, " +
                         $"Avg={avgPitch:F1}Hz, Range={range:F1}Hz, " +
                         $"Samples={validPitches.Count}/{nativePitchData.Count} ({coverage * 100f:F1}%)");
            }
            else
            {
                DebugLog($"  Confidence >= {threshold:F1}: No valid pitch data");
            }
        }
        
        // Zusätzliche Statistiken
        var allValidPitches = nativePitchData.Where(p => p.HasPitch).ToList();
        if (allValidPitches.Count > 0)
        {
            var confidenceStats = allValidPitches.Select(p => p.confidence);
            DebugLog($"  Overall confidence: Min={confidenceStats.Min():F3}, " +
                     $"Max={confidenceStats.Max():F3}, Avg={confidenceStats.Average():F3}");
            
            // Empfehlungen für gute Thresholds
            var mediumConfidencePitches = allValidPitches.Where(p => p.confidence >= 0.3f).ToList();
            var highConfidencePitches = allValidPitches.Where(p => p.confidence >= 0.5f).ToList();
            var veryHighConfidencePitches = allValidPitches.Where(p => p.confidence >= 0.7f).ToList();
            
            DebugLog($"  THRESHOLD RECOMMENDATIONS:");
            DebugLog($"    0.3: Keeps {mediumConfidencePitches.Count}/{allValidPitches.Count} " +
                     $"({mediumConfidencePitches.Count * 100f / allValidPitches.Count:F1}%) - Good balance");
            DebugLog($"    0.5: Keeps {highConfidencePitches.Count}/{allValidPitches.Count} " +
                     $"({highConfidencePitches.Count * 100f / allValidPitches.Count:F1}%) - High quality");
            DebugLog($"    0.7: Keeps {veryHighConfidencePitches.Count}/{allValidPitches.Count} " +
                     $"({veryHighConfidencePitches.Count * 100f / allValidPitches.Count:F1}%) - Very high quality");
            
            // Intelligente Empfehlung basierend auf Datenqualität
            if (mediumConfidencePitches.Count >= allValidPitches.Count * 0.8f)
            {
                DebugLog($"  RECOMMENDATION: Use threshold 0.3 (retains 80%+ of data with good quality)");
            }
            else if (highConfidencePitches.Count >= allValidPitches.Count * 0.6f)
            {
                DebugLog($"  RECOMMENDATION: Use threshold 0.5 (retains 60%+ of data with high quality)");
            }
            else
            {
                DebugLog($"  RECOMMENDATION: Use threshold 0.2 or lower (audio quality may be poor)");
            }
        }
        
        DebugLog("=== END PITCH RANGE ANALYSIS ===");
    }

    private void OnUserPitchDetected(PitchDataPoint pitchData)
    {
        if (isChorusingActive && userVisualizer != null)
        {
            userVisualizer.AddRealtimePitchData(pitchData);
            
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
    public bool HasAudioStarted => hasAudioStarted;
    public float GetNativePlaybackTime() => nativeAudioSource != null ? nativeAudioSource.time : 0f;
    public List<PitchDataPoint> GetNativePitchData() => nativePitchData;
    public float GetQuantizedSilenceDuration() => quantizedSilenceDuration;
    public float GetInitialAudioDelay() => initialAudioDelay;

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
        
        if (nativeVisualizer != null)
        {
            nativeVisualizer.OnAudioLoopTrigger -= TriggerAudioLoop;
        }
    }
}