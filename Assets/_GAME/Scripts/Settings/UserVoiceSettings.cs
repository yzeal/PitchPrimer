using UnityEngine;
using System;

[CreateAssetMenu(fileName = "UserVoiceSettings", menuName = "Pitch Trainer/User Voice Settings")]
public class UserVoiceSettings : ScriptableObject
{
    [Header("Voice Calibration")]
    [Tooltip("User's calibrated minimum pitch from English phrases")]
    public float calibratedMinPitch = 100f;
    
    [Tooltip("User's calibrated maximum pitch from English phrases")]
    public float calibratedMaxPitch = 300f;
    
    [Tooltip("Timestamp when calibration was performed")]
    public string calibrationDate = "";
    
    [Header("Voice Type Classification")]
    [Tooltip("Automatically detected voice type")]
    public VoiceType detectedVoiceType = VoiceType.Unknown;
    
    [Tooltip("User's preferred voice type override")]
    public VoiceType preferredVoiceType = VoiceType.Auto;
    
    [Header("Calibration Metadata")]
    [Tooltip("Number of samples used for calibration")]
    public int calibrationSampleCount = 0;
    
    [Tooltip("Quality score of calibration (0-1)")]
    public float calibrationQuality = 0f;
    
    [Tooltip("Languages used for calibration")]
    public string[] calibrationLanguages = { "English" };
    
    [Header("Japanese Pitch Mapping")]
    [Tooltip("Mapped range for Japanese pronunciation")]
    public float japaneseMinPitch = 100f;
    
    [Tooltip("Mapped range for Japanese pronunciation")]
    public float japaneseMaxPitch = 300f;
    
    public enum VoiceType
    {
        Unknown,
        Auto,           // Use detected type
        MaleAdult,      // 80-250Hz
        FemaleAdult,    // 120-350Hz
        Child,          // 200-500Hz
        MaleDeep,       // 60-200Hz
        FemaleLow       // 100-280Hz
    }
    
    // Validation and helper methods
    public bool IsCalibrated => calibrationSampleCount > 0 && calibratedMaxPitch > calibratedMinPitch;
    
    public float GetEffectiveMinPitch()
    {
        return IsCalibrated ? calibratedMinPitch : GetDefaultMinPitch();
    }
    
    public float GetEffectiveMaxPitch()
    {
        return IsCalibrated ? calibratedMaxPitch : GetDefaultMaxPitch();
    }
    
    public float GetDefaultMinPitch()
    {
        var voiceType = preferredVoiceType == VoiceType.Auto ? detectedVoiceType : preferredVoiceType;
        return voiceType switch
        {
            VoiceType.MaleAdult => 80f,
            VoiceType.FemaleAdult => 120f,
            VoiceType.Child => 200f,
            VoiceType.MaleDeep => 60f,
            VoiceType.FemaleLow => 100f,
            _ => 100f
        };
    }
    
    public float GetDefaultMaxPitch()
    {
        var voiceType = preferredVoiceType == VoiceType.Auto ? detectedVoiceType : preferredVoiceType;
        return voiceType switch
        {
            VoiceType.MaleAdult => 250f,
            VoiceType.FemaleAdult => 350f,
            VoiceType.Child => 500f,
            VoiceType.MaleDeep => 200f,
            VoiceType.FemaleLow => 280f,
            _ => 300f
        };
    }
    
    public void ApplyCalibrationResults(float minPitch, float maxPitch, int sampleCount, float quality)
    {
        calibratedMinPitch = minPitch;
        calibratedMaxPitch = maxPitch;
        calibrationSampleCount = sampleCount;
        calibrationQuality = quality;
        calibrationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        // Auto-detect voice type based on pitch range
        DetectVoiceType();
        
        // Map to Japanese range (initially same as calibrated range)
        japaneseMinPitch = minPitch;
        japaneseMaxPitch = maxPitch;
    }
    
    private void DetectVoiceType()
    {
        float avgPitch = (calibratedMinPitch + calibratedMaxPitch) / 2f;
        
        if (avgPitch >= 300f)
        {
            detectedVoiceType = VoiceType.Child;
        }
        else if (avgPitch >= 200f)
        {
            detectedVoiceType = VoiceType.FemaleAdult;
        }
        else if (avgPitch >= 150f)
        {
            detectedVoiceType = calibratedMaxPitch > 300f ? VoiceType.FemaleAdult : VoiceType.MaleAdult;
        }
        else if (avgPitch < 130f)
        {
            detectedVoiceType = VoiceType.MaleDeep;
        }
        else
        {
            detectedVoiceType = VoiceType.MaleAdult;
        }
    }
    
    // Japanese-specific pitch mapping
    public void MapToJapanesePitch(float nativeMinPitch, float nativeMaxPitch)
    {
        // Simple proportional mapping for now
        float userRange = calibratedMaxPitch - calibratedMinPitch;
        float nativeRange = nativeMaxPitch - nativeMinPitch;
        
        // Center user's range around native range
        float userCenter = (calibratedMinPitch + calibratedMaxPitch) / 2f;
        float nativeCenter = (nativeMinPitch + nativeMaxPitch) / 2f;
        
        // Map with some expansion for natural variation
        float expansionFactor = 1.2f;
        float mappedRange = nativeRange * expansionFactor;
        
        japaneseMinPitch = Mathf.Max(50f, userCenter - (mappedRange / 2f));
        japaneseMaxPitch = Mathf.Min(600f, userCenter + (mappedRange / 2f));
    }
}