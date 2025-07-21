using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class VoiceRangeCalibrator : MonoBehaviour
{
    [Header("Calibration Settings")]
    [SerializeField] private float calibrationDuration = 30f;
    [SerializeField] private float confidenceThreshold = 0.3f;
    [SerializeField] private int minimumSamples = 50;
    [SerializeField] private float phraseDuration = 5f;
    
    [Header("Audio Analysis")]
    [SerializeField] private MicAnalysisRefactored micAnalysis;  // ? UPDATED: Use refactored version
    
    [Header("UI References")]
    [SerializeField] private TMPro.TextMeshProUGUI instructionText;
    [SerializeField] private TMPro.TextMeshProUGUI phraseText;
    [SerializeField] private UnityEngine.UI.Slider progressSlider;
    [SerializeField] private UnityEngine.UI.Button startButton;
    [SerializeField] private UnityEngine.UI.Button skipButton;
    [SerializeField] private UnityEngine.UI.Button testButton;
    
    private List<float> calibrationPitches = new List<float>();
    private bool isCalibrating = false;
    private Coroutine calibrationCoroutine;
    
    // NEW: Store latest pitch data from events
    private float latestPitch = 0f;
    private float latestConfidence = 0f;
    
    // Calibration phrases optimized for pitch range detection
    private string[] calibrationPhrases = {
        "Hello, how are you today?",           // Natural conversation
        "What a beautiful morning!",           // Exclamation (higher pitch)
        "I'm really excited about this.",      // Emotion (varied pitch)
        "That sounds good to me.",             // Agreement (lower tones)
        "Oh no, that's terrible!",             // Surprise (pitch jumps)
        "Please count from one to ten."        // Steady enumeration
    };
    
    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(StartCalibration);
            
        if (skipButton != null)
            skipButton.onClick.AddListener(SkipCalibration);
            
        if (testButton != null)
            testButton.onClick.AddListener(QuickTest);
        
        // ? NEW: Subscribe to pitch detection events
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected += OnPitchDataReceived;
        }
            
        ShowInitialInstructions();
    }
    
    // ? NEW: Event handler for pitch data
    private void OnPitchDataReceived(PitchDataPoint pitchData)
    {
        latestPitch = pitchData.frequency;
        latestConfidence = pitchData.confidence;
        
        // Only collect during calibration and if pitch is detected with sufficient confidence
        if (isCalibrating && pitchData.HasPitch && pitchData.confidence >= confidenceThreshold)
        {
            calibrationPitches.Add(pitchData.frequency);
        }
    }
    
    private void ShowInitialInstructions()
    {
        if (instructionText != null)
        {
            instructionText.text = 
                "Voice Calibration\n\n" +
                "To provide the best learning experience, we'll calibrate your voice range.\n\n" +
                "Instructions:\n" +
                "• Read the phrases naturally in English\n" +
                "• Speak at your normal volume\n" +
                "• Use natural intonation\n" +
                "• Don't worry about perfect pronunciation\n\n" +
                "This takes about 30 seconds.";
        }
        
        if (phraseText != null)
            phraseText.text = "";
            
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(false);
        }
    }
    
    public void StartCalibration()
    {
        if (isCalibrating) return;
        
        calibrationPitches.Clear();
        latestPitch = 0f;
        latestConfidence = 0f;
        isCalibrating = true;
        
        if (startButton != null)
            startButton.gameObject.SetActive(false);
            
        if (skipButton != null)
            skipButton.gameObject.SetActive(false);
            
        if (testButton != null)
            testButton.gameObject.SetActive(false);
            
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(true);
        
        // ? UPDATED: Start analysis with refactored component
        if (micAnalysis != null)
        {
            micAnalysis.StartAnalysis();
        }
        
        calibrationCoroutine = StartCoroutine(CalibrationSequence());
    }
    
    public void SkipCalibration()
    {
        // Apply default settings based on typical voice ranges
        var settings = SettingsManager.Instance.UserVoice;
        settings.preferredVoiceType = UserVoiceSettings.VoiceType.MaleAdult; // Default assumption
        
        SettingsManager.Instance.SaveSettings();
        SettingsManager.Instance.ApplyToAllVisualizers();
        
        Debug.Log("[VoiceCalibrator] Calibration skipped, using defaults");
        
        // Transition to main scene
        LoadMainScene();
    }
    
    public void QuickTest()
    {
        // Quick test with synthetic data for development
        var testPitches = new List<float> { 120f, 140f, 160f, 180f, 200f, 220f, 240f, 260f, 280f, 300f };
        calibrationPitches.AddRange(testPitches);
        
        AnalyzeAndApplyRange();
        
        Debug.Log("[VoiceCalibrator] Quick test calibration applied");
    }
    
    private IEnumerator CalibrationSequence()
    {
        float totalTime = 0f;
        int currentPhrase = 0;
        float sequenceStartTime = Time.time;
        
        while (totalTime < calibrationDuration && currentPhrase < calibrationPhrases.Length)
        {
            // Show current phrase
            if (phraseText != null)
                phraseText.text = calibrationPhrases[currentPhrase];
                
            if (instructionText != null)
                instructionText.text = $"Please read aloud:\n\nPhrase {currentPhrase + 1} of {calibrationPhrases.Length}";
            
            // Collect data for this phrase
            float phraseStartTime = Time.time;
            while (Time.time - phraseStartTime < phraseDuration)
            {
                // ? UPDATED: Data collection now happens automatically via OnPitchDataReceived
                // No need to manually call GetCurrentPitch() - events handle it
                
                // Update progress
                totalTime = Time.time - sequenceStartTime;
                if (progressSlider != null)
                    progressSlider.value = totalTime / calibrationDuration;
                
                yield return null;
            }
            
            currentPhrase++;
            
            // Brief pause between phrases
            if (currentPhrase < calibrationPhrases.Length)
            {
                if (phraseText != null)
                    phraseText.text = "...";
                yield return new WaitForSeconds(1f);
            }
        }
        
        // Analysis complete
        AnalyzeAndApplyRange();
    }
    
    private void AnalyzeAndApplyRange()
    {
        if (calibrationPitches.Count < minimumSamples)
        {
            Debug.LogWarning($"[VoiceCalibrator] Insufficient samples: {calibrationPitches.Count} < {minimumSamples}");
            SkipCalibration();
            return;
        }
        
        // Statistical analysis with outlier removal
        var sortedPitches = calibrationPitches.OrderBy(p => p).ToList();
        
        // Remove bottom/top 10% as outliers
        int removeCount = Mathf.RoundToInt(sortedPitches.Count * 0.1f);
        var cleanedPitches = sortedPitches
            .Skip(removeCount)
            .Take(sortedPitches.Count - 2 * removeCount)
            .ToList();
        
        if (cleanedPitches.Count < minimumSamples / 2)
        {
            Debug.LogWarning("[VoiceCalibrator] Too many outliers removed");
            SkipCalibration();
            return;
        }
        
        float minPitch = cleanedPitches.Min();
        float maxPitch = cleanedPitches.Max();
        
        // Add 15% buffer for natural variation in Japanese
        float range = maxPitch - minPitch;
        float bufferMinPitch = Mathf.Max(50f, minPitch - (range * 0.15f));
        float bufferMaxPitch = Mathf.Min(800f, maxPitch + (range * 0.15f));
        
        // Calculate quality score
        float quality = Mathf.Clamp01(cleanedPitches.Count / (float)calibrationPitches.Count);
        
        // Apply calibration results
        var settings = SettingsManager.Instance.UserVoice;
        settings.ApplyCalibrationResults(bufferMinPitch, bufferMaxPitch, cleanedPitches.Count, quality);
        
        SettingsManager.Instance.SaveSettings();
        SettingsManager.Instance.ApplyToAllVisualizers();
        
        // Show results
        ShowCalibrationResults(bufferMinPitch, bufferMaxPitch, cleanedPitches.Count, quality);
        
        Debug.Log($"[VoiceCalibrator] Calibration complete: {bufferMinPitch:F1}-{bufferMaxPitch:F1}Hz ({cleanedPitches.Count} samples, quality: {quality:F2})");
        
        // Transition to main scene after showing results
        StartCoroutine(DelayedSceneTransition(3f));
    }
    
    private void ShowCalibrationResults(float minPitch, float maxPitch, int sampleCount, float quality)
    {
        if (instructionText != null)
        {
            instructionText.text = 
                "Calibration Complete!\n\n" +
                $"Your voice range: {minPitch:F0}-{maxPitch:F0} Hz\n" +
                $"Voice type: {SettingsManager.Instance.UserVoice.detectedVoiceType}\n" +
                $"Quality: {quality * 100:F0}%\n" +
                $"Samples: {sampleCount}\n\n" +
                "Loading training scene...";
        }
        
        if (phraseText != null)
            phraseText.text = "";
            
        if (progressSlider != null)
            progressSlider.value = 1f;
    }
    
    private IEnumerator DelayedSceneTransition(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadMainScene();
    }
    
    private void LoadMainScene()
    {
        // ? UPDATED: Stop analysis with refactored component
        if (micAnalysis != null)
            micAnalysis.StopAnalysis();
        
        string mainSceneName = "TestScene2";
        
        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
        catch (System.ArgumentException)
        {
            Debug.LogWarning($"[VoiceCalibrator] Scene '{mainSceneName}' not found in build settings.");
            Debug.LogWarning("[VoiceCalibrator] Staying in current scene");
        }
    }
    
    void OnDestroy()
    {
        // ? NEW: Unsubscribe from events
        if (micAnalysis != null)
        {
            micAnalysis.OnPitchDetected -= OnPitchDataReceived;
            micAnalysis.StopAnalysis();
        }
        
        if (calibrationCoroutine != null)
            StopCoroutine(calibrationCoroutine);
    }
    
    // ? NEW: Optional debug display for current pitch
    void OnGUI()
    {
        if (isCalibrating && micAnalysis != null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"Latest Pitch: {latestPitch:F1} Hz");
            GUILayout.Label($"Confidence: {latestConfidence:F3}");
            GUILayout.Label($"Samples Collected: {calibrationPitches.Count}");
            GUILayout.Label($"Is Analyzing: {micAnalysis.IsAnalyzing}");
            GUILayout.EndArea();
        }
    }
}