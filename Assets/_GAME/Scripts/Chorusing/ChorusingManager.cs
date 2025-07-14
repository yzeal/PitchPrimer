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

    // ENHANCED: Extended debugging for drift analysis
    private float expectedLoopStartTime = 0f;
    private int loopCount = 0;
    private float totalDriftAccumulated = 0f;

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

    // FIXED: Calculate silence duration for perfect visual sync
    private void CalculateQuantizedSilence()
    {
        if (nativeClip == null || nativePitchData == null)
        {
            DebugLog("No native clip or pitch data available for quantization calculation");
            quantizedSilenceDuration = 0f;
            return;
        }

        // CRITICAL FIX: Calculate total cubes first, then derive silence
        int actualAudioCubes = nativePitchData.Count;
        int requestedSilenceCubes = Mathf.RoundToInt(requestedSilenceDuration / analysisInterval);
        
        // CRITICAL: Ensure minimum 1 silence cube to avoid drift
        if (requestedSilenceCubes == 0)
        {
            requestedSilenceCubes = 1;
            DebugLog("ANTI-DRIFT: Minimum 1 silence cube enforced to prevent timing drift");
        }
        
        int totalCubes = actualAudioCubes + requestedSilenceCubes;
        
        // Calculate times for perfect visual sync
        float visualAudioTime = actualAudioCubes * analysisInterval;
        float visualTotalTime = totalCubes * analysisInterval;
        
        // Calculate silence to make total time match visual total time
        float actualSilenceNeeded = visualTotalTime - nativeClip.length;
        quantizedSilenceDuration = actualSilenceNeeded;
        
        // FIXED: Handle negative silence by adding one analysis interval
        if (quantizedSilenceDuration < 0f)
        {
            quantizedSilenceDuration += analysisInterval;
            totalCubes += 1; // Add one more silence cube
            requestedSilenceCubes += 1;
            visualTotalTime = totalCubes * analysisInterval;
            
            DebugLog($"ANTI-DRIFT: Negative silence detected, added 1 analysis interval");
            DebugLog($"  Original silence needed: {actualSilenceNeeded:F3}s");
            DebugLog($"  Adjusted silence: {quantizedSilenceDuration:F3}s");
            DebugLog($"  Added silence cubes: 1");
            DebugLog($"  New total cubes: {totalCubes}");
        }
        
        DebugLog($"PERFECT SYNC QUANTIZATION CALCULATION:");
        DebugLog($"  Audio clip length: {nativeClip.length:F3}s (actual file)");
        DebugLog($"  Actual audio cubes: {actualAudioCubes} (from PitchAnalyzer)");
        DebugLog($"  Visual audio time: {visualAudioTime:F3}s");
        DebugLog($"  Requested silence: {requestedSilenceDuration:F3}s");
        DebugLog($"  Requested silence cubes: {requestedSilenceCubes}");
        DebugLog($"  Total cubes: {totalCubes}");
        DebugLog($"  Visual total time: {visualTotalTime:F3}s");
        DebugLog($"  Quantized silence: {quantizedSilenceDuration:F3}s");
        DebugLog($"  Analysis interval: {analysisInterval:F3}s");
        
        // Verify perfect sync
        float actualTotalTime = nativeClip.length + quantizedSilenceDuration;
        
        DebugLog($"  VERIFICATION: Visual total time: {visualTotalTime:F3}s");
        DebugLog($"  VERIFICATION: Actual total time: {actualTotalTime:F3}s");
        DebugLog($"  VERIFICATION: Perfect match: {Mathf.Approximately(visualTotalTime, actualTotalTime)}");
        DebugLog($"  VERIFICATION: Time difference: {(actualTotalTime - visualTotalTime):F6}s");
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

    // NEW: Update-based quantized silence logic with detailed timing analysis
    private void UpdateQuantizedAudioSilenceLogic()
    {
        if (nativeAudioSource == null || nativeClip == null) return;

        float currentTime = Time.time;
        
        // CRITICAL FIX: Use visual-based timing for perfect sync
        float visualAudioTime = (nativePitchData?.Count ?? 0) * analysisInterval;
        float visualTotalLoopTime = visualAudioTime + quantizedSilenceDuration;

        if (!isInSilencePeriod)
        {
            // FIXED: Use visual audio time, not actual clip length
            float visualAudioEndTime = lastLoopEndTime - nativeClip.length + visualAudioTime;
            
            if (currentTime >= visualAudioEndTime)
            {
                // ENHANCED: Measure actual vs expected timing
                float actualSilenceStartTime = currentTime;
                float expectedSilenceStartTime = chorusingStartTime + (loopCount * visualTotalLoopTime) + visualAudioTime;
                float silenceStartDrift = actualSilenceStartTime - expectedSilenceStartTime;
                totalDriftAccumulated += silenceStartDrift;
                
                // Enter QUANTIZED silence period
                isInSilencePeriod = true;
                silenceStartTime = currentTime;
                
                DebugLog($"?? ENTERING SILENCE - DRIFT ANALYSIS:");
                DebugLog($"  Loop #{loopCount} - Audio clip length: {nativeClip.length:F3}s");
                DebugLog($"  Visual audio time: {visualAudioTime:F3}s");
                DebugLog($"  Visual total loop: {visualTotalLoopTime:F3}s");
                DebugLog($"  Expected silence start: {expectedSilenceStartTime:F3}s");
                DebugLog($"  Actual silence start: {actualSilenceStartTime:F3}s");
                DebugLog($"  THIS LOOP drift: {silenceStartDrift:F3}s");
                DebugLog($"  TOTAL accumulated drift: {totalDriftAccumulated:F3}s");
                DebugLog($"  Audio was playing for: {(currentTime - (lastLoopEndTime - nativeClip.length)):F3}s");
                DebugLog($"  AudioSource.time when stopped: {nativeAudioSource.time:F3}s");
            }
        }
        else
        {
            // Check if QUANTIZED silence period should end
            if (currentTime >= silenceStartTime + quantizedSilenceDuration)
            {
                // ENHANCED: Measure audio restart timing
                float actualRestartTime = currentTime;
                float expectedRestartTime = chorusingStartTime + ((loopCount + 1) * visualTotalLoopTime);
                float restartDrift = actualRestartTime - expectedRestartTime;
                
                // FIXED: Next loop starts at quantized time
                isInSilencePeriod = false;
                
                // CRITICAL: Log BEFORE starting audio to avoid timing confusion
                float timeBeforePlay = Time.time;
                nativeAudioSource.Play();
                float timeAfterPlay = Time.time;
                float audioStartDelay = timeAfterPlay - timeBeforePlay;
                
                // CRITICAL: Use visual total loop time for next loop timing
                lastLoopEndTime = currentTime + nativeClip.length;
                loopCount++;
                
                DebugLog($"?? STARTING AUDIO - DRIFT ANALYSIS:");
                DebugLog($"  Loop #{loopCount} - Silence duration: {quantizedSilenceDuration:F3}s");
                DebugLog($"  Expected restart time: {expectedRestartTime:F3}s");
                DebugLog($"  Actual restart time: {actualRestartTime:F3}s");
                DebugLog($"  RESTART drift: {restartDrift:F3}s");
                DebugLog($"  Audio.Play() delay: {audioStartDelay:F6}s");
                DebugLog($"  Next loop will end at: {lastLoopEndTime:F3}s");
                DebugLog($"  Visual timing: {visualTotalLoopTime:F3}s per loop");
                DebugLog($"  TOTAL drift after {loopCount} loops: {totalDriftAccumulated:F3}s");
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

    // ENHANCED: More detailed visualization timing
    private void UpdateNativeVisualization()
    {
        if (nativeVisualizer != null && nativePitchData != null && nativeAudioSource != null)
        {
            float playbackTime;
            // CRITICAL FIX: Use consistent visual timing throughout
            float visualTotalLoop = (nativePitchData.Count * analysisInterval) + quantizedSilenceDuration;

            if (isInSilencePeriod)
            {
                // During QUANTIZED silence: extend beyond audio duration
                float silenceElapsed = Time.time - silenceStartTime;
                // FIXED: Use visual audio time instead of actual clip length
                float visualAudioTime = nativePitchData.Count * analysisInterval;
                playbackTime = visualAudioTime + silenceElapsed;
            }
            else
            {
                // During audio: use actual audio time
                playbackTime = nativeAudioSource.time;
            }

            nativeVisualizer.UpdateNativeTrackPlayback(playbackTime, nativePitchData);
            
            // ENHANCED: More frequent and detailed debug timing
            if (enableDebugLogging && Time.time % 1f < 0.1f) // Every 1 second instead of 2
            {
                float expectedPlaybackTime = (Time.time - chorusingStartTime) % visualTotalLoop;
                float timingDifference = playbackTime - expectedPlaybackTime;
                
                DebugLog($"?? VISUALIZATION TIMING:");
                DebugLog($"  AudioSource.isPlaying: {nativeAudioSource.isPlaying}");
                DebugLog($"  In silence period: {isInSilencePeriod}");
                DebugLog($"  Actual playback time: {playbackTime:F3}s");
                DebugLog($"  Expected playback time: {expectedPlaybackTime:F3}s");
                DebugLog($"  Visual total loop: {visualTotalLoop:F3}s");
                // CRITICAL FIX: Use same visual timing for comparison
                DebugLog($"  Audio total loop: {visualTotalLoop:F3}s"); // Was: {(nativeClip.length + quantizedSilenceDuration):F3}s
                DebugLog($"  Timing difference: {timingDifference:F3}s");
                DebugLog($"  Elapsed since start: {(Time.time - chorusingStartTime):F1}s");
                DebugLog($"  Current loop: {loopCount}");
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