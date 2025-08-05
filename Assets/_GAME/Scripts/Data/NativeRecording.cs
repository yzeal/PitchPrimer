using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "NativeRecording", menuName = "Japanese Trainer/Native Recording", order = 1)]
public class NativeRecording : ScriptableObject
{
    [Header("Audio Data")]
    [SerializeField] private AudioClip audioClip;
    
    [Header("Pitch Analysis Data")]
    [SerializeField] private float pitchRangeMin = 80f;   // Hz
    [SerializeField] private float pitchRangeMax = 400f;  // Hz
    [SerializeField] private bool autoCalculatePitchRange = true;
    [Tooltip("Allows manual override of calculated pitch range")]
    [SerializeField] private bool allowManualPitchAdjustment = true;
    
    [Header("Text Content")]
    [SerializeField] [TextArea(2, 4)] private string kanjiText;
    [SerializeField] [TextArea(2, 4)] private string kanaText;
    [SerializeField] [TextArea(2, 4)] private string romajiText;
    
    [Header("Speaker Metadata")]
    [SerializeField] private string speakerName;
    [SerializeField] private NativeRecordingGender speakerGender = NativeRecordingGender.NotSpecified;
    [SerializeField] private string speakerNotes; // Optional additional info
    
    [Header("Recording Metadata")]
    [SerializeField] private string recordingName;
    [SerializeField] [TextArea(1, 3)] private string description;
    [SerializeField] private int difficultyLevel = 1;
    [SerializeField] private NativeRecordingType recordingType = NativeRecordingType.Sentence;
    
    [Header("Analysis Settings & Cache")]
    [SerializeField] private PitchAnalysisSettings analysisSettings;
    [SerializeField] private bool persistAnalysisData = true;
    [Tooltip("Cached pitch analysis data - automatically populated")]
    [SerializeField] private List<NativeRecordingPitchDataPoint> cachedPitchData = new List<NativeRecordingPitchDataPoint>();
    [SerializeField] private bool isAnalyzed = false;
    [SerializeField] private string analysisVersion = "1.0"; // Track analysis version for migration
    
    // Runtime pitch data (converted from serializable format)
    private List<PitchDataPoint> runtimePitchData;
    private bool runtimeDataLoaded = false;
    
    // Public Properties
    public AudioClip AudioClip => audioClip;
    public float PitchRangeMin => pitchRangeMin;
    public float PitchRangeMax => pitchRangeMax;
    public string KanjiText => kanjiText;
    public string KanaText => kanaText;
    public string RomajiText => romajiText;
    public string SpeakerName => speakerName;
    public NativeRecordingGender SpeakerGender => speakerGender;
    public string SpeakerNotes => speakerNotes;
    public string RecordingName => recordingName;
    public string Description => description;
    public int DifficultyLevel => difficultyLevel;
    public NativeRecordingType Type => recordingType;
    public bool IsAnalyzed => isAnalyzed;
    public int CachedDataPoints => cachedPitchData?.Count ?? 0;
    
    /// <summary>
    /// Gets the analyzed pitch data, using cached data if available
    /// </summary>
    public List<PitchDataPoint> GetPitchData(float analysisInterval = 0.1f)
    {
        // Use cached data if available
        if (isAnalyzed && cachedPitchData != null && cachedPitchData.Count > 0)
        {
            if (!runtimeDataLoaded)
            {
                LoadRuntimeDataFromCache();
            }
            return runtimePitchData;
        }
        
        // Fall back to analysis if no cached data
        Debug.LogWarning($"[NativeRecording] No cached pitch data for {name}, performing analysis...");
        AnalyzeAudio(analysisInterval);
        return runtimePitchData;
    }
    
    /// <summary>
    /// Performs pitch analysis and caches the results
    /// </summary>
    public void AnalyzeAudio(float analysisInterval = 0.1f)
    {
        if (audioClip == null)
        {
            Debug.LogWarning($"[NativeRecording] No audio clip assigned for {name}");
            return;
        }
        
        Debug.Log($"[NativeRecording] Analyzing audio for {name}...");
        
        // Use existing PitchAnalyzer with settings
        if (analysisSettings == null)
        {
            // Create default settings (PitchAnalysisSettings is NOT a ScriptableObject)
            analysisSettings = new PitchAnalysisSettings();
        }
        
        var rawPitchData = PitchAnalyzer.PreAnalyzeAudioClip(audioClip, analysisSettings, analysisInterval);
        
        if (analysisSettings.useSmoothing && rawPitchData != null)
        {
            rawPitchData = PitchAnalyzer.SmoothPitchData(rawPitchData, analysisSettings.historySize);
        }
        
        // Store runtime data
        runtimePitchData = rawPitchData;
        runtimeDataLoaded = true;
        
        // Cache the data if persistence is enabled
        if (persistAnalysisData && rawPitchData != null)
        {
            CacheAnalysisData(rawPitchData);
        }
        
        // Auto-calculate pitch range if enabled (but allow manual override)
        if (autoCalculatePitchRange)
        {
            var calculatedMin = pitchRangeMin;
            var calculatedMax = pitchRangeMax;
            
            CalculatePitchRange(out calculatedMin, out calculatedMax);
            
            // Only update if manual adjustment is disabled OR values haven't been manually set
            if (!allowManualPitchAdjustment || (!HasManualPitchAdjustment()))
            {
                pitchRangeMin = calculatedMin;
                pitchRangeMax = calculatedMax;
                
                // Mark as dirty for Unity Editor
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                #endif
            }
            else
            {
                Debug.Log($"[NativeRecording] Calculated pitch range {calculatedMin:F1}Hz - {calculatedMax:F1}Hz, " +
                         $"but keeping manual values {pitchRangeMin:F1}Hz - {pitchRangeMax:F1}Hz");
            }
        }
        
        isAnalyzed = true;
        Debug.Log($"[NativeRecording] Analysis complete for {name}: {rawPitchData?.Count ?? 0} data points");
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    /// <summary>
    /// Caches analysis data in serializable format
    /// </summary>
    private void CacheAnalysisData(List<PitchDataPoint> data)
    {
        cachedPitchData.Clear();
        
        foreach (var point in data)
        {
            cachedPitchData.Add(new NativeRecordingPitchDataPoint(point));
        }
        
        Debug.Log($"[NativeRecording] Cached {cachedPitchData.Count} pitch data points for {name}");
    }
    
    /// <summary>
    /// Loads runtime data from cached serializable data
    /// </summary>
    private void LoadRuntimeDataFromCache()
    {
        if (cachedPitchData == null || cachedPitchData.Count == 0)
        {
            runtimePitchData = new List<PitchDataPoint>();
            return;
        }
        
        runtimePitchData = new List<PitchDataPoint>();
        
        foreach (var serializable in cachedPitchData)
        {
            runtimePitchData.Add(serializable.ToPitchDataPoint());
        }
        
        runtimeDataLoaded = true;
        Debug.Log($"[NativeRecording] Loaded {runtimePitchData.Count} cached pitch data points for {name}");
    }
    
    /// <summary>
    /// Calculates pitch range from current data
    /// </summary>
    private void CalculatePitchRange(out float minPitch, out float maxPitch)
    {
        minPitch = 80f;  // Fallback values
        maxPitch = 400f;
        
        var currentData = runtimePitchData ?? GetPitchData();
        if (currentData == null || currentData.Count == 0) return;
        
        // Filter for high-confidence pitch data
        var validPitches = currentData
            .Where(p => p.HasPitch && p.confidence >= 0.3f)
            .Select(p => p.frequency)
            .ToList();
        
        if (validPitches.Count > 0)
        {
            minPitch = validPitches.Min();
            maxPitch = validPitches.Max();
            
            Debug.Log($"[NativeRecording] Calculated pitch range for {name}: " +
                     $"{minPitch:F1}Hz - {maxPitch:F1}Hz " +
                     $"({validPitches.Count}/{currentData.Count} samples)");
        }
    }
    
    /// <summary>
    /// Manually sets pitch range (overrides auto-calculation)
    /// </summary>
    public void SetManualPitchRange(float minPitch, float maxPitch)
    {
        if (minPitch >= maxPitch)
        {
            Debug.LogWarning($"[NativeRecording] Invalid pitch range: min ({minPitch}) >= max ({maxPitch})");
            return;
        }
        
        pitchRangeMin = minPitch;
        pitchRangeMax = maxPitch;
        
        Debug.Log($"[NativeRecording] Manual pitch range set for {name}: {minPitch:F1}Hz - {maxPitch:F1}Hz");
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    /// <summary>
    /// Checks if pitch range has been manually adjusted from defaults
    /// </summary>
    private bool HasManualPitchAdjustment()
    {
        // Simple heuristic: if values are not the default 80-400 range
        return !(Mathf.Approximately(pitchRangeMin, 80f) && Mathf.Approximately(pitchRangeMax, 400f));
    }
    
    /// <summary>
    /// Forces re-analysis and clears cached data
    /// </summary>
    public void ForceReanalyze(float analysisInterval = 0.1f)
    {
        isAnalyzed = false;
        cachedPitchData.Clear();
        runtimePitchData = null;
        runtimeDataLoaded = false;
        
        AnalyzeAudio(analysisInterval);
    }
    
    /// <summary>
    /// Clears cached analysis data (keeps settings)
    /// </summary>
    public void ClearCachedData()
    {
        cachedPitchData.Clear();
        runtimePitchData = null;
        runtimeDataLoaded = false;
        isAnalyzed = false;
        
        Debug.Log($"[NativeRecording] Cleared cached data for {name}");
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    
    /// <summary>
    /// Validates that all required data is present
    /// </summary>
    public bool IsValid()
    {
        return audioClip != null && 
               !string.IsNullOrEmpty(recordingName) &&
               !string.IsNullOrEmpty(kanjiText) &&
               !string.IsNullOrEmpty(kanaText) &&
               !string.IsNullOrEmpty(romajiText) &&
               !string.IsNullOrEmpty(speakerName);
    }
    
    /// <summary>
    /// Gets the duration of the audio clip
    /// </summary>
    public float GetDuration()
    {
        return audioClip != null ? audioClip.length : 0f;
    }
    
    /// <summary>
    /// Gets speaker info as formatted string
    /// </summary>
    public string GetSpeakerInfo()
    {
        var genderText = speakerGender switch
        {
            NativeRecordingGender.Male => "männlich",
            NativeRecordingGender.Female => "weiblich", 
            NativeRecordingGender.Diverse => "divers",
            _ => "nicht angegeben"
        };
        
        return $"{speakerName} ({genderText})";
    }
    
    // Editor validation
    void OnValidate()
    {
        // Update name if recordingName changes
        if (!string.IsNullOrEmpty(recordingName) && name != recordingName)
        {
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.RenameAsset(UnityEditor.AssetDatabase.GetAssetPath(this), recordingName);
            #endif
        }
        
        // Validate pitch range
        if (pitchRangeMin >= pitchRangeMax)
        {
            pitchRangeMax = pitchRangeMin + 100f;
        }
        
        // Ensure valid difficulty level
        difficultyLevel = Mathf.Clamp(difficultyLevel, 1, 10);
    }
}

// Supporting enums and data structures - RENAMED to avoid conflicts
[System.Serializable]
public enum NativeRecordingGender
{
    NotSpecified = 0,
    Male = 1,
    Female = 2,
    Diverse = 3
}

[System.Serializable]
public enum NativeRecordingType
{
    Word = 0,
    Phrase = 1,
    Sentence = 2,
    Paragraph = 3,
    Conversation = 4
}

/// <summary>
/// Serializable version of PitchDataPoint for Unity Inspector/Assets
/// RENAMED to avoid conflicts with existing types
/// </summary>
[System.Serializable]
public struct NativeRecordingPitchDataPoint
{
    public float timestamp;
    public float frequency;
    public float confidence;
    public float audioLevel;
    
    public NativeRecordingPitchDataPoint(PitchDataPoint original)
    {
        timestamp = original.timestamp;
        frequency = original.frequency;
        confidence = original.confidence;
        audioLevel = original.audioLevel;
    }
    
    public PitchDataPoint ToPitchDataPoint()
    {
        return new PitchDataPoint(timestamp, frequency, confidence, audioLevel);
    }
}