using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// COPILOT CONTEXT: Core scoring system for Japanese pitch accent trainer
// Handles user recording analysis, comparison with native clips, and score calculation
// Integrates with existing ChorusingManager and UserAudioRecorder via events
// NEW: Normalized scoring using UserVoiceSettings and NativeRecording pitch ranges

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
    [SerializeField] private bool useNormalizedScoring = true; // NEW: Enable/disable normalization
    
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
    [SerializeField] private float minimumRecordingRatio = 0.3f; // User recording must be at least 30% of native length
    [SerializeField] private float minimumAbsoluteLength = 1.0f; // At least 1 second
    
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
        
        // FIXED: Notify GameStateManager FIRST, then wait for canvas to be active
        OnScoringComplete?.Invoke(); // This triggers canvas activation
        
        // Wait for canvas to be activated
        yield return new WaitForSeconds(0.2f);
        
        // Step 1: Load user recording as AudioClip
        yield return StartCoroutine(LoadUserRecording(userRecordingPath));
        
        if (userRecordingClip == null)
        {
            Debug.LogError("[ScoringManager] Failed to load user recording!");
            OnScoringError?.Invoke("Failed to load your recording. Please try again.");
            yield break;
        }
        
        // Step 2: Get native clip data from ChorusingManager
        if (!GetNativeClipData())
        {
            Debug.LogError("[ScoringManager] Failed to get native clip data!");
            OnScoringError?.Invoke("Failed to load native recording data. Please restart the exercise.");
            yield break;
        }
        
        // Step 3: Analyze user recording
        AnalyzeUserRecording();
        
        // NEW: Step 4: Validate recording length
        if (!ValidateRecordingLength())
        {
            // Error message already sent via OnScoringError event
            yield break; // Don't continue with scoring
        }
        
        // Step 5: Calculate scores (only if validation passed)
        CalculateScores();
        
        // Step 6: Setup visualizations
        SetupVisualizations();
        
        // Step 7: Set scoring as active (moved to end)
        isScoringActive = true;
        
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
    
    private bool ValidateRecordingLength()
    {
        if (userRecordingClip == null || nativePitchData == null)
        {
            DebugLog("?? Cannot validate - missing clip or native data");
            return false;
        }
        
        float userDuration = userRecordingClip.length;
        float nativeDuration = nativePitchData.Count * analysisInterval;
        float durationRatio = userDuration / nativeDuration;
        
        DebugLog($"?? Recording validation: User={userDuration:F1}s, Native={nativeDuration:F1}s, Ratio={durationRatio:F2}");
        
        // Check minimum absolute length
        if (userDuration < minimumAbsoluteLength)
        {
            DebugLog($"? Recording too short: {userDuration:F1}s < {minimumAbsoluteLength:F1}s minimum");
            OnScoringError?.Invoke($"Recording too short ({userDuration:F1}s). Please record for at least {minimumAbsoluteLength:F1}s.");
            return false;
        }
        
        // Check relative length compared to native
        if (durationRatio < minimumRecordingRatio)
        {
            DebugLog($"? Recording too short relative to native: {durationRatio:F1}% < {minimumRecordingRatio * 100:F0}% required");
            OnScoringError?.Invoke($"Recording too short. Please record for at least {minimumRecordingRatio * 100:F0}% of the native duration ({nativeDuration * minimumRecordingRatio:F1}s).");
            return false;
        }
        
        DebugLog("? Recording length validation passed");
        return true;
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
    
    // NEW: Enhanced pitch scoring with normalization support
    private float CalculatePitchScore(List<PitchDataPoint> native, List<PitchDataPoint> user)
    {
        var nativePitches = native.Where(p => p.HasPitch).Select(p => p.frequency).ToList();
        var userPitches = user.Where(p => p.HasPitch).Select(p => p.frequency).ToList();
        
        if (nativePitches.Count == 0 || userPitches.Count == 0)
        {
            DebugLog("?? No pitch data available for comparison");
            return 0f;
        }
        
        float correlation;
        
        if (useNormalizedScoring)
        {
            correlation = CalculateNormalizedPitchScore(nativePitches, userPitches);
        }
        else
        {
            // Fallback to old method
            correlation = CalculateCorrelation(nativePitches, userPitches);
        }
        
        // Convert correlation to 0-100 score
        float score = Mathf.Clamp01(correlation) * 100f;
        
        DebugLog($"?? Pitch scoring: {nativePitches.Count} vs {userPitches.Count} points, " +
                $"correlation={correlation:F3}, score={score:F1} (normalized: {useNormalizedScoring})");
        
        return score;
    }
    
    // NEW: Normalized pitch scoring using calibration data
    private float CalculateNormalizedPitchScore(List<float> nativePitches, List<float> userPitches)
    {
        // Get native recording pitch range
        var nativeRecording = chorusingManager.GetCurrentRecording();
        if (nativeRecording == null)
        {
            DebugLog("?? No native recording available, falling back to direct correlation");
            return CalculateCorrelation(nativePitches, userPitches);
        }
        
        // Get user calibration data
        var userVoiceSettings = SettingsManager.Instance?.UserVoice;
        if (userVoiceSettings == null || !userVoiceSettings.IsCalibrated)
        {
            DebugLog("?? User not calibrated, falling back to direct correlation");
            return CalculateCorrelation(nativePitches, userPitches);
        }
        
        // Get pitch ranges
        float nativeMin = nativeRecording.PitchRangeMin;
        float nativeMax = nativeRecording.PitchRangeMax;
        float userMin = userVoiceSettings.GetEffectiveMinPitch();
        float userMax = userVoiceSettings.GetEffectiveMaxPitch();
        
        DebugLog($"?? Normalizing pitch data:");
        DebugLog($"   Native range: {nativeMin:F1}Hz - {nativeMax:F1}Hz");
        DebugLog($"   User range: {userMin:F1}Hz - {userMax:F1}Hz");
        
        // Normalize both datasets to 0-1 range
        var normalizedNative = NormalizePitches(nativePitches, nativeMin, nativeMax);
        var normalizedUser = NormalizePitches(userPitches, userMin, userMax);
        
        // Calculate correlation on normalized data
        float correlation = CalculateCorrelation(normalizedNative, normalizedUser);
        
        DebugLog($"?? Normalized correlation: {correlation:F3}");
        
        // NEW: Additional scoring factors for better accuracy
        float rangeMatchScore = CalculateRangeMatchScore(nativePitches, userPitches, nativeMin, nativeMax, userMin, userMax);
        float contourScore = CalculateContourScore(normalizedNative, normalizedUser);
        
        // Weighted combination of different scoring factors
        float finalScore = (correlation * 0.5f) + (rangeMatchScore * 0.3f) + (contourScore * 0.2f);
        
        DebugLog($"?? Scoring breakdown: Correlation={correlation:F3}, Range={rangeMatchScore:F3}, " +
                $"Contour={contourScore:F3}, Final={finalScore:F3}");
        
        return finalScore;
    }
    
    // NEW: Normalize pitch values to 0-1 range
    private List<float> NormalizePitches(List<float> pitches, float minPitch, float maxPitch)
    {
        if (maxPitch <= minPitch || pitches.Count == 0) 
        {
            DebugLog($"?? Invalid pitch range: {minPitch:F1}Hz - {maxPitch:F1}Hz, returning original data");
            return pitches; // Avoid division by zero
        }
        
        var normalized = pitches.Select(p => Mathf.Clamp01((p - minPitch) / (maxPitch - minPitch))).ToList();
        
        DebugLog($"?? Normalized {pitches.Count} pitches from [{minPitch:F1}Hz-{maxPitch:F1}Hz] to [0-1]");
        
        return normalized;
    }
    
    // NEW: Calculate how well user's pitch range usage matches native speaker
    private float CalculateRangeMatchScore(List<float> nativePitches, List<float> userPitches, 
                                          float nativeMin, float nativeMax, float userMin, float userMax)
    {
        // Calculate actual range usage
        float nativeUsedMin = nativePitches.Min();
        float nativeUsedMax = nativePitches.Max();
        float userUsedMin = userPitches.Min();
        float userUsedMax = userPitches.Max();
        
        // Normalize to their respective ranges
        float nativeUsedRangeNorm = (nativeUsedMax - nativeUsedMin) / (nativeMax - nativeMin);
        float userUsedRangeNorm = (userUsedMax - userUsedMin) / (userMax - userMin);
        
        // Score based on how similarly they use their available range
        float rangeSimilarity = 1f - Mathf.Abs(nativeUsedRangeNorm - userUsedRangeNorm);
        
        DebugLog($"?? Range usage: Native={nativeUsedRangeNorm:F3}, User={userUsedRangeNorm:F3}, " +
                $"Similarity={rangeSimilarity:F3}");
        
        return Mathf.Clamp01(rangeSimilarity);
    }
    
    // NEW: Calculate pitch contour similarity (pattern matching)
    private float CalculateContourScore(List<float> normalizedNative, List<float> normalizedUser)
    {
        if (normalizedNative.Count < 2 || normalizedUser.Count < 2)
            return 0f;
        
        // Calculate pitch direction changes (contour)
        var nativeContour = CalculatePitchContour(normalizedNative);
        var userContour = CalculatePitchContour(normalizedUser);
        
        if (nativeContour.Count == 0 || userContour.Count == 0)
            return 0f;
        
        // Compare contours with Dynamic Time Warping approximation
        float contourSimilarity = CalculateContourSimilarity(nativeContour, userContour);
        
        DebugLog($"?? Contour analysis: Native segments={nativeContour.Count}, " +
                $"User segments={userContour.Count}, Similarity={contourSimilarity:F3}");
        
        return contourSimilarity;
    }
    
    // NEW: Extract pitch movement patterns
    private List<float> CalculatePitchContour(List<float> pitches)
    {
        var contour = new List<float>();
        
        for (int i = 1; i < pitches.Count; i++)
        {
            float change = pitches[i] - pitches[i - 1];
            contour.Add(change);
        }
        
        return contour;
    }
    
    // NEW: Compare contour patterns
    private float CalculateContourSimilarity(List<float> contour1, List<float> contour2)
    {
        // Simple approach: resample to same length and calculate correlation
        int minLength = Mathf.Min(contour1.Count, contour2.Count);
        if (minLength < 2) return 0f;
        
        var resampled1 = ResampleList(contour1, minLength);
        var resampled2 = ResampleList(contour2, minLength);
        
        return CalculateCorrelation(resampled1, resampled2);
    }
    
    // NEW: Resample list to target length
    private List<float> ResampleList(List<float> source, int targetLength)
    {
        if (source.Count == targetLength) return new List<float>(source);
        if (source.Count == 0) return new List<float>();
        
        var resampled = new List<float>();
        for (int i = 0; i < targetLength; i++)
        {
            float index = (float)i / (targetLength - 1) * (source.Count - 1);
            int intIndex = Mathf.FloorToInt(index);
            float fraction = index - intIndex;
            
            if (intIndex >= source.Count - 1)
            {
                resampled.Add(source[source.Count - 1]);
            }
            else
            {
                // Linear interpolation
                float value = source[intIndex] * (1f - fraction) + source[intIndex + 1] * fraction;
                resampled.Add(value);
            }
        }
        
        return resampled;
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
    
    // NEW: Settings control
    public void SetNormalizedScoring(bool enabled)
    {
        useNormalizedScoring = enabled;
        DebugLog($"?? Normalized scoring: {(enabled ? "Enabled" : "Disabled")}");
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

// NOTE: Supporting data structures are now in separate files:
// - ScoringResults.cs
// - SpeechSegment.cs  
// - ScoringSettings.cs