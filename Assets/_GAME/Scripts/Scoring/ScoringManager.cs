using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// COPILOT CONTEXT: Core scoring system for Japanese pitch accent trainer
// Handles user recording analysis, comparison with native clips, and score calculation
// Integrates with existing ChorusingManager and UserAudioRecorder via events

public class ScoringManager : MonoBehaviour
{
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
    
    void Start()
    {
        InitializeComponents();
    }
    
    void Update()
    {
        if (isScoringActive)
        {
            UpdatePlaybackStates();
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
            DebugLog("? Subscribed to UserAudioRecorder.OnRecordingSaved");
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
        DebugLog($"??? User recording saved: {recordingFilePath}");
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
            yield break;
        }
        
        // Step 2: Get native clip data from ChorusingManager
        if (!GetNativeClipData())
        {
            Debug.LogError("[ScoringManager] Failed to get native clip data!");
            yield break;
        }
        
        // Step 3: Analyze user recording
        AnalyzeUserRecording();
        
        // Step 4: Calculate scores
        CalculateScores();
        
        // Step 5: Setup visualizations
        SetupVisualizations();
        
        // Step 6: Notify completion
        isScoringActive = true;
        OnScoringComplete?.Invoke();
        
        DebugLog("? Scoring process complete!");
    }
    
    private IEnumerator LoadUserRecording(string filePath)
    {
        DebugLog($"?? Loading user recording: {filePath}");
        
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[ScoringManager] User recording file not found: {filePath}");
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
                
                DebugLog($"? User recording loaded: {userRecordingClip.length:F1}s, {userRecordingClip.samples} samples");
            }
            else
            {
                Debug.LogError($"[ScoringManager] Failed to load user recording: {www.error}");
            }
        }
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
    }
    
    private void CalculateScores()
    {
        DebugLog("?? Calculating scores...");
        
        if (nativePitchData == null || userPitchData == null)
        {
            Debug.LogError("[ScoringManager] Missing pitch data for scoring!");
            return;
        }
        
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
    
    private float CalculatePitchScore(List<PitchDataPoint> native, List<PitchDataPoint> user)
    {
        // Simple pitch comparison algorithm (placeholder)
        // TODO: Implement more sophisticated scoring like DTW
        
        var nativePitches = native.Where(p => p.HasPitch).Select(p => p.frequency).ToList();
        var userPitches = user.Where(p => p.HasPitch).Select(p => p.frequency).ToList();
        
        if (nativePitches.Count == 0 || userPitches.Count == 0)
        {
            DebugLog("?? No pitch data available for comparison");
            return 0f;
        }
        
        // Calculate correlation between pitch curves
        float correlation = CalculateCorrelation(nativePitches, userPitches);
        
        // Convert correlation to 0-100 score
        float score = Mathf.Clamp01(correlation) * 100f;
        
        DebugLog($"?? Pitch scoring: {nativePitches.Count} vs {userPitches.Count} points, correlation={correlation:F3}, score={score:F1}");
        
        return score;
    }
    
    private float CalculateRhythmScore(List<PitchDataPoint> native, List<PitchDataPoint> user)
    {
        // Simple rhythm comparison algorithm (placeholder)
        // Compare speaking pace and timing patterns
        
        var nativeSpeechSegments = GetSpeechSegments(native);
        var userSpeechSegments = GetSpeechSegments(user);
        
        if (nativeSpeechSegments.Count == 0 || userSpeechSegments.Count == 0)
        {
            DebugLog("?? No speech segments found for rhythm analysis");
            return 0f;
        }
        
        // Calculate average segment duration difference
        float nativeAvgDuration = nativeSpeechSegments.Average(s => s.duration);
        float userAvgDuration = userSpeechSegments.Average(s => s.duration);
        
        float durationDifference = Mathf.Abs(nativeAvgDuration - userAvgDuration);
        float maxDifference = Mathf.Max(nativeAvgDuration, userAvgDuration);
        
        float rhythmAccuracy = 1f - (durationDifference / maxDifference);
        float score = Mathf.Clamp01(rhythmAccuracy) * 100f;
        
        DebugLog($"?? Rhythm scoring: Native={nativeAvgDuration:F2}s, User={userAvgDuration:F2}s, difference={durationDifference:F2}s, score={score:F1}");
        
        return score;
    }
    
    private float CalculateCorrelation(List<float> data1, List<float> data2)
    {
        // Simple correlation calculation
        // Resample to same length for comparison
        int minLength = Mathf.Min(data1.Count, data2.Count);
        if (minLength < 2) return 0f;
        
        float sum1 = 0f, sum2 = 0f, sum1Sq = 0f, sum2Sq = 0f, sumProducts = 0f;
        
        for (int i = 0; i < minLength; i++)
        {
            float val1 = data1[i * data1.Count / minLength];
            float val2 = data2[i * data2.Count / minLength];
            
            sum1 += val1;
            sum2 += val2;
            sum1Sq += val1 * val1;
            sum2Sq += val2 * val2;
            sumProducts += val1 * val2;
        }
        
        float numerator = (minLength * sumProducts) - (sum1 * sum2);
        float denominator = Mathf.Sqrt(((minLength * sum1Sq) - (sum1 * sum1)) * ((minLength * sum2Sq) - (sum2 * sum2)));
        
        return denominator != 0f ? numerator / denominator : 0f;
    }
    
    private List<SpeechSegment> GetSpeechSegments(List<PitchDataPoint> pitchData)
    {
        var segments = new List<SpeechSegment>();
        bool inSpeech = false;
        float segmentStart = 0f;
        
        for (int i = 0; i < pitchData.Count; i++)
        {
            bool hasPitch = pitchData[i].HasPitch;
            
            if (!inSpeech && hasPitch)
            {
                // Start of speech segment
                inSpeech = true;
                segmentStart = pitchData[i].timestamp;
            }
            else if (inSpeech && !hasPitch)
            {
                // End of speech segment
                inSpeech = false;
                float duration = pitchData[i].timestamp - segmentStart;
                if (duration > 0.1f) // Minimum segment duration
                {
                    segments.Add(new SpeechSegment { start = segmentStart, duration = duration });
                }
            }
        }
        
        // Handle case where audio ends during speech
        if (inSpeech && pitchData.Count > 0)
        {
            float duration = pitchData.Last().timestamp - segmentStart;
            if (duration > 0.1f)
            {
                segments.Add(new SpeechSegment { start = segmentStart, duration = duration });
            }
        }
        
        return segments;
    }
    
    private void SetupVisualizations()
    {
        DebugLog("?? Setting up visualizations...");
        
        // Setup native visualizer with existing data
        if (nativeVisualizer != null && nativePitchData != null)
        {
            nativeVisualizer.ClearAll();
            // Note: We'll need to add a method to PitchVisualizer for static display
            DebugLog($"?? Native visualization ready: {nativePitchData.Count} points");
        }
        
        // Setup user visualizer with analyzed data
        if (userVisualizer != null && userPitchData != null)
        {
            userVisualizer.ClearAll();
            // Note: We'll need to add a method to PitchVisualizer for static display
            DebugLog($"?? User visualization ready: {userPitchData.Count} points");
        }
        
        // Setup audio sources
        if (nativeAudioSource != null && chorusingManager != null)
        {
            nativeAudioSource.clip = chorusingManager.GetComponent<AudioSource>()?.clip;
        }
        
        if (userAudioSource != null && userRecordingClip != null)
        {
            userAudioSource.clip = userRecordingClip;
        }
        
        // Notify UI that clips are ready
        OnClipsLoaded?.Invoke(nativeAudioSource.clip, userAudioSource.clip);
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

// NOTE: Supporting data structures are now in separate files:
// - ScoringResults.cs
// - SpeechSegment.cs  
// - ScoringSettings.cs