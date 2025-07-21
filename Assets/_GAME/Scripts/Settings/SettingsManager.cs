using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    private static SettingsManager _instance;
    public static SettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // FIXED: Use FindFirstObjectByType instead of deprecated FindObjectOfType
                _instance = FindFirstObjectByType<SettingsManager>();
                
                if (_instance == null)
                {
                    // Create new instance
                    GameObject go = new GameObject("SettingsManager");
                    _instance = go.AddComponent<SettingsManager>();
                }
                
                DontDestroyOnLoad(_instance.gameObject);
            }
            return _instance;
        }
    }
    
    [Header("Settings References")]
    [SerializeField] private UserVoiceSettings _userVoiceSettings;
    
    // Public access
    public UserVoiceSettings UserVoice => _userVoiceSettings;
    
    // PlayerPrefs keys
    private const string VOICE_MIN_PITCH_KEY = "UserVoice_MinPitch";
    private const string VOICE_MAX_PITCH_KEY = "UserVoice_MaxPitch";
    private const string VOICE_SAMPLE_COUNT_KEY = "UserVoice_SampleCount";
    private const string VOICE_QUALITY_KEY = "UserVoice_Quality";
    private const string VOICE_DATE_KEY = "UserVoice_Date";
    private const string VOICE_TYPE_KEY = "UserVoice_Type";
    private const string VOICE_PREFERRED_TYPE_KEY = "UserVoice_PreferredType";
    private const string JAPANESE_MIN_PITCH_KEY = "UserVoice_JapaneseMin";
    private const string JAPANESE_MAX_PITCH_KEY = "UserVoice_JapaneseMax";
    
    void Awake()
    {
        // Singleton pattern
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSettings();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeSettings()
    {
        // Create default settings if none exist
        if (_userVoiceSettings == null)
        {
            // FIXED: Correct syntax for ScriptableObject.CreateInstance
            _userVoiceSettings = ScriptableObject.CreateInstance<UserVoiceSettings>();
        }
        
        // Load saved settings
        LoadSettings();
        
        Debug.Log($"[SettingsManager] Initialized - Calibrated: {_userVoiceSettings.IsCalibrated}");
    }
    
    public void SaveSettings()
    {
        if (_userVoiceSettings == null) return;
        
        // Save to PlayerPrefs (works on all platforms)
        PlayerPrefs.SetFloat(VOICE_MIN_PITCH_KEY, _userVoiceSettings.calibratedMinPitch);
        PlayerPrefs.SetFloat(VOICE_MAX_PITCH_KEY, _userVoiceSettings.calibratedMaxPitch);
        PlayerPrefs.SetInt(VOICE_SAMPLE_COUNT_KEY, _userVoiceSettings.calibrationSampleCount);
        PlayerPrefs.SetFloat(VOICE_QUALITY_KEY, _userVoiceSettings.calibrationQuality);
        PlayerPrefs.SetString(VOICE_DATE_KEY, _userVoiceSettings.calibrationDate);
        PlayerPrefs.SetInt(VOICE_TYPE_KEY, (int)_userVoiceSettings.detectedVoiceType);
        PlayerPrefs.SetInt(VOICE_PREFERRED_TYPE_KEY, (int)_userVoiceSettings.preferredVoiceType);
        PlayerPrefs.SetFloat(JAPANESE_MIN_PITCH_KEY, _userVoiceSettings.japaneseMinPitch);
        PlayerPrefs.SetFloat(JAPANESE_MAX_PITCH_KEY, _userVoiceSettings.japaneseMaxPitch);
        
        PlayerPrefs.Save(); // Ensure immediate save
        
        Debug.Log($"[SettingsManager] Settings saved - Range: {_userVoiceSettings.calibratedMinPitch:F1}-{_userVoiceSettings.calibratedMaxPitch:F1}Hz");
    }
    
    public void LoadSettings()
    {
        if (_userVoiceSettings == null) return;
        
        // Load from PlayerPrefs
        if (PlayerPrefs.HasKey(VOICE_MIN_PITCH_KEY))
        {
            _userVoiceSettings.calibratedMinPitch = PlayerPrefs.GetFloat(VOICE_MIN_PITCH_KEY);
            _userVoiceSettings.calibratedMaxPitch = PlayerPrefs.GetFloat(VOICE_MAX_PITCH_KEY);
            _userVoiceSettings.calibrationSampleCount = PlayerPrefs.GetInt(VOICE_SAMPLE_COUNT_KEY);
            _userVoiceSettings.calibrationQuality = PlayerPrefs.GetFloat(VOICE_QUALITY_KEY);
            _userVoiceSettings.calibrationDate = PlayerPrefs.GetString(VOICE_DATE_KEY);
            _userVoiceSettings.detectedVoiceType = (UserVoiceSettings.VoiceType)PlayerPrefs.GetInt(VOICE_TYPE_KEY);
            _userVoiceSettings.preferredVoiceType = (UserVoiceSettings.VoiceType)PlayerPrefs.GetInt(VOICE_PREFERRED_TYPE_KEY);
            _userVoiceSettings.japaneseMinPitch = PlayerPrefs.GetFloat(JAPANESE_MIN_PITCH_KEY, _userVoiceSettings.calibratedMinPitch);
            _userVoiceSettings.japaneseMaxPitch = PlayerPrefs.GetFloat(JAPANESE_MAX_PITCH_KEY, _userVoiceSettings.calibratedMaxPitch);
            
            Debug.Log($"[SettingsManager] Settings loaded - Range: {_userVoiceSettings.calibratedMinPitch:F1}-{_userVoiceSettings.calibratedMaxPitch:F1}Hz, Type: {_userVoiceSettings.detectedVoiceType}");
        }
        else
        {
            Debug.Log("[SettingsManager] No saved settings found, using defaults");
        }
    }
    
    // Convenience methods for applying settings to visualizers
    public void ApplyToVisualizer(PitchVisualizer visualizer)
    {
        if (visualizer == null || _userVoiceSettings == null) return;
        
        float minPitch = _userVoiceSettings.GetEffectiveMinPitch();
        float maxPitch = _userVoiceSettings.GetEffectiveMaxPitch();
        
        visualizer.SetPersonalPitchRange(minPitch, maxPitch);
        
        Debug.Log($"[SettingsManager] Applied settings to {visualizer.gameObject.name}: {minPitch:F1}-{maxPitch:F1}Hz");
    }
    
    public void ApplyJapaneseMappingToVisualizer(PitchVisualizer visualizer)
    {
        if (visualizer == null || _userVoiceSettings == null) return;
        
        visualizer.SetPersonalPitchRange(_userVoiceSettings.japaneseMinPitch, _userVoiceSettings.japaneseMaxPitch);
        
        Debug.Log($"[SettingsManager] Applied Japanese mapping to {visualizer.gameObject.name}: {_userVoiceSettings.japaneseMinPitch:F1}-{_userVoiceSettings.japaneseMaxPitch:F1}Hz");
    }
    
    public void ApplyToAllVisualizers(bool useJapaneseMapping = false)
    {
        // FIXED: Use FindObjectsByType instead of deprecated FindObjectsOfType
        var visualizers = FindObjectsByType<PitchVisualizer>(FindObjectsSortMode.None);
        foreach (var visualizer in visualizers)
        {
            if (useJapaneseMapping)
                ApplyJapaneseMappingToVisualizer(visualizer);
            else
                ApplyToVisualizer(visualizer);
        }
        
        Debug.Log($"[SettingsManager] Applied settings to {visualizers.Length} visualizers (Japanese mapping: {useJapaneseMapping})");
    }
    
    // Reset settings (for testing or new user)
    public void ResetSettings()
    {
        if (_userVoiceSettings == null) return;
        
        _userVoiceSettings.calibratedMinPitch = 100f;
        _userVoiceSettings.calibratedMaxPitch = 300f;
        _userVoiceSettings.calibrationSampleCount = 0;
        _userVoiceSettings.calibrationQuality = 0f;
        _userVoiceSettings.calibrationDate = "";
        _userVoiceSettings.detectedVoiceType = UserVoiceSettings.VoiceType.Unknown;
        _userVoiceSettings.preferredVoiceType = UserVoiceSettings.VoiceType.Auto;
        _userVoiceSettings.japaneseMinPitch = 100f;
        _userVoiceSettings.japaneseMaxPitch = 300f;
        
        SaveSettings();
        
        Debug.Log("[SettingsManager] Settings reset to defaults");
    }
    
    // Auto-save on mobile platforms
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveSettings();
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveSettings();
    }
    
    void OnDestroy()
    {
        SaveSettings();
    }
}