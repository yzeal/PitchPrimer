using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JapanesePitchTrainer.Scoring.Advanced;

// COPILOT CONTEXT: Core scoring system for Japanese pitch accent trainer
// Handles user recording analysis, comparison with native clips, and score calculation
// Integrates with existing ChorusingManager and UserAudioRecorder via events
// NEW: Advanced DTW and Energy-based scoring algorithms

public class ScoringManager : MonoBehaviour
{
    // Add this new event near the other events
    public System.Action<string> OnScoringError; // NEW: For error messages like "Recording too short"
    
    [Header("?? SCORING MANAGER - Audio Comparison & Analysis")]
    [Space(10)]
    
    [Header("Component References")]
    [SerializeField] private ChorusingManager chorusingManager;
    [SerializeField] private UserAudioRecorder userRecorder;
    [SerializeField] private PitchVisualizer nativeVisualizer;
    [SerializeField] private PitchVisualizer userVisualizer;
    
    [Header("Audio Playback")]
    [SerializeField] private AudioSource nativeAudioSource;
    [SerializeField] private AudioSource userAudioSource;
    
    [Header("Analysis Settings")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private float analysisInterval = 0.1f;
    
    [Header("Scoring Configuration")]
    [SerializeField] private ScoringSettings scoringSettings;
    
    [Header("Advanced Scoring Settings")]
    [SerializeField] private AdvancedScoringAlgorithms.AdvancedScoringSettings advancedSettings;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    
    // Events for GameStateManager integration
    public System.Action OnScoringComplete;
    public System.Action OnRetryRequested;
    public System.Action OnNextRequested;
    
    // Events for UI updates
    public System.Action<float, float> OnScoresCalculated; // pitch score, rhythm score
    public System.Action<AudioClip, AudioClip> OnClipsLoaded; // native clip, user clip
    public System.Action<bool> OnNativeClipPlaybackChanged; // is playing
    public System.Action<bool> OnUserClipPlaybackChanged; // is playing
    
    // Internal state
    private bool isScoringActive = false;
    private AudioClip userRecordingClip;
    private List<PitchDataPoint> nativePitchData;
    private List<PitchDataPoint> userPitchData;
    private ScoringResults currentScores;
    
    // Playback state
    private bool isNativeClipPlaying = false;
    private bool isUserClipPlaying = false;
    
    [Header("Validation Settings")]
    [SerializeField] private float minimumRecordingRatio = 0.75f;
    [SerializeField] private float minimumAbsoluteLength = 1.0f;
    
    void Start()
    {
        InitializeComponents();
        InitializeAdvancedSettings();
    }
    
    void Update()
    {
        if (isScoringActive)
        {
            UpdatePlaybackStates();
        }
    }
    
    private void InitializeAdvancedSettings()
    {
        // Initialize advanced scoring settings if not set
        if (advancedSettings.Equals(default(AdvancedScoringAlgorithms.AdvancedScoringSettings)))
        {
            advancedSettings = AdvancedScoringAlgorithms.AdvancedScoringSettings.Default;
            DebugLog("?? Initialized default advanced scoring settings");
        }
    }
    
    private void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (chorusingManager == null)
            chorusingManager = FindFirstObjectByType<ChorusingManager>();
        
        if (userRecorder == null)
            userRecorder = FindFirstObjectByType<UserAudioRecorder>();
        
        // Setup AudioSources
        if (nativeAudioSource == null)
        {
            var go = new GameObject("NativeAudioSource");
            go.transform.SetParent(transform);
            nativeAudioSource = go.AddComponent<AudioSource>();
        }
        
        if (userAudioSource == null)
        {
            var go = new GameObject("UserAudioSource");
            go.transform.SetParent(transform);
            userAudioSource = go.AddComponent<AudioSource>();
        }
        
        ConfigureAudioSources();
        
        // Subscribe to events
        if (userRecorder != null)
        {
            userRecorder.OnRecordingSaved += OnUserRecordingSaved;
            DebugLog("?? Subscribed to UserAudioRecorder.OnRecordingSaved");
        }
        else
        {
            Debug.LogError("[ScoringManager] UserAudioRecorder not found!");
        }
        
        DebugLog("ScoringManager initialized");
    }
    
    private void ConfigureAudioSources()
    {
        // Configure native audio source
        nativeAudioSource.playOnAwake = false;
        nativeAudioSource.loop = false;
        nativeAudioSource.volume = 1f;
        
        // Configure user audio source
        userAudioSource.playOnAwake = false;
        userAudioSource.loop = false;
        userAudioSource.volume = 1f;
    }
    
    private void OnUserRecordingSaved(string recordingFilePath)
    {
        DebugLog($"?? User recording saved: {recordingFilePath}");
        StartCoroutine(StartScoringProcess(recordingFilePath));
    }
    
    private IEnumerator StartScoringProcess(string userRecordingPath)
    {
        DebugLog("?? Starting scoring process...");
        
        // Step 1: Load user recording as AudioClip
        yield return StartCoroutine(LoadUserRecording(userRecordingPath));
        
        if (userRecordingClip == null)
        {
            Debug.LogError("[ScoringManager] Failed to load user recording!");
            OnScoringError?.Invoke("Failed to load your recording. Please try again.");
            yield break;
        }
        
        // Step 2: Enhanced audio clip validation
        if (!ValidateAudioClip(userRecordingClip))
        {
            yield break; // Error message already sent
        }
        
        // Step 3: Get native clip data from ChorusingManager
        if (!GetNativeClipData())
        {
            Debug.LogError("[ScoringManager] Failed to get native clip data!");
            OnScoringError?.Invoke("Failed to load native recording data. Please restart the exercise.");
            yield break;
        }
        
        // Step 4: Analyze user recording
        AnalyzeUserRecording();
        
        // Step 5: Enhanced analysis validation
        if (!ValidateAnalysisResults())
        {
            yield break; // Error message already sent
        }
        
        // Step 6: Validate recording length
        if (!ValidateRecordingLength())
        {
            DebugLog("?? Recording too short for scoring - staying in chorusing mode for smooth UX");
            yield break; // Don't continue with scoring, no error shown
        }
        
        // Step 7: ONLY HERE: OnScoringComplete after all validations passed!
        DebugLog("? All validations passed - starting successful scoring");
        OnScoringComplete?.Invoke(); // This triggers canvas activation
        
        // Wait for canvas to be activated
        yield return new WaitForSeconds(0.2f);
        
        // Step 8: Calculate scores (only if validation passed)
        CalculateScores();
        
        // Step 9: Setup visualizations
        SetupVisualizations();
        
        // Step 10: Set scoring as active (moved to end)
        isScoringActive = true;
        
        DebugLog("?? Scoring process complete!");
    }
    
    private IEnumerator LoadUserRecording(string filePath)
    {
        DebugLog($"?? Loading user recording: {filePath}");
        
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[ScoringManager] User recording file not found: {filePath}");
            OnScoringError?.Invoke("Recording file not found. Please try recording again.");
            yield break;
        }
        
        // Check file size
        var fileInfo = new FileInfo(filePath);
        DebugLog($"?? File size: {fileInfo.Length} bytes ({fileInfo.Length / 1024f:F1} KB)");
        
        if (fileInfo.Length < 1000) // Less than 1KB is suspicious
        {
            DebugLog($"?? Very small file size: {fileInfo.Length} bytes - possible empty recording");
            OnScoringError?.Invoke("Recording appears to be empty. Please check your microphone and try again.");
            yield break;
        }
        
        // Use Unity's UnityWebRequest to load WAV file
        string fileUrl = "file://" + filePath;
        using (var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                userRecordingClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                userRecordingClip.name = "UserRecording";
                
                DebugLog($"? User recording loaded: {userRecordingClip.length:F1}s, {userRecordingClip.samples} samples, {userRecordingClip.channels} channels");
            }
            else
            {
                Debug.LogError($"[ScoringManager] Failed to load user recording: {www.error}");
                OnScoringError?.Invoke("Failed to load recording file. Please try again.");
            }
        }
    }
    
    private bool ValidateAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            DebugLog("? Audio clip is null");
            OnScoringError?.Invoke("Failed to load audio data. Please try recording again.");
            return false;
        }
        
        if (clip.length <= 0f)
        {
            DebugLog($"? Audio clip has no duration: {clip.length}s");
            OnScoringError?.Invoke("Recording has no duration. Please try recording again.");
            return false;
        }
        
        if (clip.samples <= 0)
        {
            DebugLog($"? Audio clip has no samples: {clip.samples}");
            OnScoringError?.Invoke("Recording contains no audio data. Please check your microphone and try again.");
            return false;
        }
        
        // Check if audio data contains actual sound
        float[] audioData = new float[clip.samples * clip.channels];
        clip.GetData(audioData, 0);
        
        // Calculate RMS (Root Mean Square) to check for actual audio content
        float sumSquares = 0f;
        for (int i = 0; i < audioData.Length; i++)
        {
            sumSquares += audioData[i] * audioData[i];
        }
        float rms = Mathf.Sqrt(sumSquares / audioData.Length);
        
        DebugLog($"?? Audio analysis: Length={clip.length:F1}s, Samples={clip.samples}, RMS={rms:F6}");
        
        if (rms < 0.001f) // Very quiet threshold
        {
            DebugLog($"? Audio appears to be silent (RMS: {rms:F6})");
            OnScoringError?.Invoke("Recording appears to be silent. Please check your microphone volume and try again.");
            return false;
        }
        
        DebugLog($"? Audio clip validation passed: Duration={clip.length:F1}s, RMS={rms:F6}");
        return true;
    }
    
    private bool GetNativeClipData()
    {
        if (chorusingManager == null)
        {
            Debug.LogError("[ScoringManager] ChorusingManager not found!");
            return false;
        }
        
        nativePitchData = chorusingManager.GetNativePitchData();
        
        if (nativePitchData == null || nativePitchData.Count == 0)
        {
            Debug.LogError("[ScoringManager] No native pitch data available!");
            return false;
        }
        
        DebugLog($"?? Native pitch data: {nativePitchData.Count} points");
        return true;
    }
    
    private void AnalyzeUserRecording()
    {
        DebugLog("?? Analyzing user recording...");
        
        userPitchData = PitchAnalyzer.PreAnalyzeAudioClip(userRecordingClip, analysisSettings, analysisInterval);
        
        if (analysisSettings.useSmoothing)
        {
            userPitchData = PitchAnalyzer.SmoothPitchData(userPitchData, analysisSettings.historySize);
        }
        
        var stats = PitchAnalyzer.CalculateStatistics(userPitchData);
        DebugLog($"?? User recording analysis: {stats}");
        
        if (userPitchData != null)
        {
            int pitchPointsWithData = userPitchData.Count(p => p.HasPitch);
            float pitchDataPercentage = userPitchData.Count > 0 ? (float)pitchPointsWithData / userPitchData.Count * 100f : 0f;
            
            DebugLog($"?? Analysis details: Total points={userPitchData.Count}, " +
                    $"With pitch={pitchPointsWithData}, Percentage={pitchDataPercentage:F1}%");
        }
        else
        {
            DebugLog("? Analysis returned null pitch data");
        }
    }
    
    private bool ValidateAnalysisResults()
    {
        if (userPitchData == null)
        {
            DebugLog("? Pitch analysis returned null data");
            OnScoringError?.Invoke("Failed to analyze recording. Please try again.");
            return false;
        }
        
        if (userPitchData.Count == 0)
        {
            DebugLog("? Pitch analysis returned empty data");
            OnScoringError?.Invoke("No audio data found in recording. Please check your microphone and try again.");
            return false;
        }
        
        // Check if we have any actual pitch data
        int pitchPointsWithData = userPitchData.Count(p => p.HasPitch);
        float pitchDataPercentage = (float)pitchPointsWithData / userPitchData.Count * 100f;
        
        DebugLog($"?? Pitch data analysis: {pitchPointsWithData}/{userPitchData.Count} points have pitch data ({pitchDataPercentage:F1}%)");
        
        if (pitchPointsWithData == 0)
        {
            DebugLog("? No pitch data found in any analysis points");
            OnScoringError?.Invoke("No speech detected in recording. Please speak clearly and try again.");
            return false;
        }
        
        if (pitchDataPercentage < 5f) // Less than 5% contains speech
        {
            DebugLog($"? Very little speech detected: {pitchDataPercentage:F1}%");
            OnScoringError?.Invoke($"Very little speech detected ({pitchDataPercentage:F0}%). Please speak more clearly and try again.");
            return false;
        }
        
        DebugLog($"? Analysis validation passed: {pitchDataPercentage:F1}% of recording contains speech");
        return true;
    }
    
    private bool ValidateRecordingLength()
    {
        if (userRecordingClip == null || nativePitchData == null)
        {
            DebugLog("?? Cannot validate length - missing clip or native data");
            OnScoringError?.Invoke("Internal error: Missing recording data. Please try again.");
            return false;
        }
        
        float userDuration = userRecordingClip.length;
        float nativeDuration = nativePitchData.Count * analysisInterval;
        float durationRatio = userDuration / nativeDuration;
        
        DebugLog($"?? Recording validation: User={userDuration:F1}s, Native={nativeDuration:F1}s, Ratio={durationRatio:F2}");
        
        // Check minimum absolute length
        if (userDuration < minimumAbsoluteLength)
        {
            DebugLog($"?? Recording below minimum absolute length: {userDuration:F1}s < {minimumAbsoluteLength:F1}s - staying in chorusing");
            return false;
        }
        
        // Check relative length compared to native
        if (durationRatio < minimumRecordingRatio)
        {
            DebugLog($"?? Recording below minimum ratio: {durationRatio * 100:F0}% < {minimumRecordingRatio * 100:F0}% required - staying in chorusing");
            return false;
        }
        
        DebugLog($"? Recording length validation passed: {durationRatio * 100:F0}% of native duration");
        return true;
    }
    
    private void CalculateScores()
    {
        DebugLog("?? Calculating advanced scores...");
        
        if (nativePitchData == null || userPitchData == null)
        {
            Debug.LogError("[ScoringManager] Missing pitch data for scoring!");
            OnScoringError?.Invoke("Internal error: Missing analysis data. Please try again.");
            return;
        }
        
        // Use advanced scoring algorithms
        float pitchScore = CalculatePitchScore(nativePitchData, userPitchData);
        float rhythmScore = CalculateRhythmScore(nativePitchData, userPitchData);
        
        currentScores = new ScoringResults
        {
            pitchScore = pitchScore,
            rhythmScore = rhythmScore,
            overallScore = (pitchScore + rhythmScore) / 2f,
            nativeDataPoints = nativePitchData.Count,
            userDataPoints = userPitchData.Count
        };
        
        DebugLog($"?? Scores calculated: Pitch={pitchScore:F1}, Rhythm={rhythmScore:F1}, Overall={currentScores.overallScore:F1}");
        
        // Notify UI
        OnScoresCalculated?.Invoke(pitchScore, rhythmScore);
    }
    
    // NEW: Enhanced pitch scoring with DTW analysis - ONLY VERSION
    private float CalculatePitchScore(List<PitchDataPoint> native, List<PitchDataPoint> user)
    {
        DebugLog("?? Calculating DTW-based pitch score...");
        
        try
        {
            // Use advanced DTW algorithm
            float score = AdvancedScoringAlgorithms.CalculateDTWPitchScore(native, user, advancedSettings);
            
            DebugLog($"?? DTW Pitch score: {score:F1}%");
            return score;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoringManager] Error in DTW pitch scoring: {e.Message}");
            return 0f;
        }
    }
    
    // NEW: Enhanced rhythm scoring with energy-based segmentation - ONLY VERSION
    private float CalculateRhythmScore(List<PitchDataPoint> native, List<PitchDataPoint> user)
    {
        DebugLog("?? Calculating prosodic rhythm score...");
        
        try
        {
            // Use advanced energy-based rhythm analysis
            float score = AdvancedScoringAlgorithms.CalculateProsodicRhythmScore(native, user, advancedSettings);
            
            DebugLog($"?? Prosodic rhythm score: {score:F1}%");
            return score;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScoringManager] Error in rhythm scoring: {e.Message}");
            return 0f;
        }
    }
    
    private void SetupVisualizations()
    {
        DebugLog("?? Setting up visualizations...");
        
        // Setup static visualizations for scoring comparison
        if (nativeVisualizer != null && userVisualizer != null && 
            nativePitchData != null && userPitchData != null)
        {
            // Display static pitch data for comparison
            nativeVisualizer.DisplayStaticPitchData(nativePitchData);
            userVisualizer.DisplayStaticPitchData(userPitchData);
            
            DebugLog($"?? Static visualizations created: Native={nativePitchData.Count}, User={userPitchData.Count} points");
        }
        else
        {
            Debug.LogWarning("[ScoringManager] Missing visualizers or pitch data for static display");
        }
        
        // Setup audio sources
        if (nativeAudioSource != null && chorusingManager != null)
        {
            var chorusingAudioSource = chorusingManager.GetComponent<AudioSource>();
            if (chorusingAudioSource != null)
            {
                nativeAudioSource.clip = chorusingAudioSource.clip;
            }
        }
        
        if (userAudioSource != null && userRecordingClip != null)
        {
            userAudioSource.clip = userRecordingClip;
        }
        
        // Notify UI that clips are ready
        OnClipsLoaded?.Invoke(nativeAudioSource.clip, userAudioSource.clip);
        
        DebugLog("? Scoring visualizations setup complete");
    }
    
    private void UpdatePlaybackStates()
    {
        // Monitor native audio playback
        bool nativePlaying = nativeAudioSource != null && nativeAudioSource.isPlaying;
        if (nativePlaying != isNativeClipPlaying)
        {
            isNativeClipPlaying = nativePlaying;
            OnNativeClipPlaybackChanged?.Invoke(isNativeClipPlaying);
        }
        
        // Monitor user audio playback
        bool userPlaying = userAudioSource != null && userAudioSource.isPlaying;
        if (userPlaying != isUserClipPlaying)
        {
            isUserClipPlaying = userPlaying;
            OnUserClipPlaybackChanged?.Invoke(isUserClipPlaying);
        }
    }
    
    // Public API for UI controls
    public void PlayNativeClip()
    {
        if (nativeAudioSource != null && nativeAudioSource.clip != null)
        {
            nativeAudioSource.Play();
            DebugLog("?? Playing native clip");
        }
    }
    
    public void StopNativeClip()
    {
        if (nativeAudioSource != null)
        {
            nativeAudioSource.Stop();
            DebugLog("?? Stopped native clip");
        }
    }

    public void PlayUserClip()
    {
        if (userAudioSource != null && userAudioSource.clip != null)
        {
            userAudioSource.Play();
            DebugLog("?? Playing user clip");
        }
    }

    public void StopUserClip()
    {
        if (userAudioSource != null)
        {
            userAudioSource.Stop();
            DebugLog("?? Stopped user clip");
        }
    }

    public void RequestRetry()
    {
        DebugLog("?? Retry requested");
        StopScoring();
        OnRetryRequested?.Invoke();
    }

    public void RequestNext()
    {
        DebugLog("?? Next requested");
        StopScoring();
        OnNextRequested?.Invoke();
    }

    public void StopScoring()
    {
        if (!isScoringActive) return;
        
        DebugLog("?? Stopping scoring...");
        
        // Stop all audio playback
        StopNativeClip();
        StopUserClip();
        
        // Clear visualizations
        if (nativeVisualizer != null)
            nativeVisualizer.ClearAll();
        
        if (userVisualizer != null)
            userVisualizer.ClearAll();
        
        // Clear data
        userRecordingClip = null;
        userPitchData = null;
        currentScores = new ScoringResults();
        
        isScoringActive = false;
        
        DebugLog("? Scoring stopped");
    }
    
    // Public getters
    public bool IsScoringActive => isScoringActive;
    public ScoringResults CurrentScores => currentScores;
    public bool IsNativeClipPlaying => isNativeClipPlaying;
    public bool IsUserClipPlaying => isUserClipPlaying;
    public AudioClip UserRecordingClip => userRecordingClip;
    
    // Public settings control
    public void UpdateAdvancedSettings(AdvancedScoringAlgorithms.AdvancedScoringSettings newSettings)
    {
        advancedSettings = newSettings;
        DebugLog("?? Advanced scoring settings updated");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[ScoringManager] {message}");
        }
    }
    
    void OnDestroy()
    {
        // Cleanup subscriptions
        if (userRecorder != null)
        {
            userRecorder.OnRecordingSaved -= OnUserRecordingSaved;
        }
        
        // Stop scoring
        if (isScoringActive)
        {
            StopScoring();
        }
    }
}