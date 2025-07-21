using UnityEngine;

public class SettingsIntegration : MonoBehaviour
{
    [Header("Auto-Apply Settings")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool useJapaneseMapping = false;
    
    [Header("Specific Visualizers (Optional)")]
    [SerializeField] private PitchVisualizer[] targetVisualizers;
    
    void Start()
    {
        if (applyOnStart)
        {
            ApplySettings();
        }
    }
    
    public void ApplySettings()
    {
        if (targetVisualizers != null && targetVisualizers.Length > 0)
        {
            // Apply to specific visualizers
            foreach (var visualizer in targetVisualizers)
            {
                if (visualizer != null)
                {
                    if (useJapaneseMapping)
                        SettingsManager.Instance.ApplyJapaneseMappingToVisualizer(visualizer);
                    else
                        SettingsManager.Instance.ApplyToVisualizer(visualizer);
                }
            }
        }
        else
        {
            // Apply to all visualizers in scene
            SettingsManager.Instance.ApplyToAllVisualizers(useJapaneseMapping);
        }
    }
    
    // Convenient methods for UI buttons
    public void ApplyCalibrationSettings()
    {
        useJapaneseMapping = false;
        ApplySettings();
    }
    
    public void ApplyJapaneseSettings()
    {
        useJapaneseMapping = true;
        ApplySettings();
    }
    
    // Debug methods
    [ContextMenu("Show Current Settings")]
    public void ShowCurrentSettings()
    {
        var settings = SettingsManager.Instance.UserVoice;
        Debug.Log($"Current Settings - Calibrated: {settings.IsCalibrated}, Range: {settings.GetEffectiveMinPitch():F1}-{settings.GetEffectiveMaxPitch():F1}Hz, Type: {settings.detectedVoiceType}");
    }
    
    [ContextMenu("Reset All Settings")]
    public void ResetAllSettings()
    {
        SettingsManager.Instance.ResetSettings();
        ApplySettings();
    }
}