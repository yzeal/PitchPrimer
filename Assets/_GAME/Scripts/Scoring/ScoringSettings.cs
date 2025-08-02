using UnityEngine;

// COPILOT CONTEXT: Scoring configuration settings for Japanese pitch accent trainer
// Configurable parameters for pitch and rhythm scoring algorithms

[System.Serializable]
public class ScoringSettings
{
    [Header("Scoring Weights")]
    [Range(0f, 1f)] public float pitchWeight = 0.6f;
    [Range(0f, 1f)] public float rhythmWeight = 0.4f;
    
    [Header("Pitch Scoring")]
    public float pitchToleranceHz = 20f;
    public float minimumConfidence = 0.3f;
    
    [Header("Rhythm Scoring")]
    public float rhythmToleranceSeconds = 0.2f;
    public float minimumSegmentDuration = 0.1f;
    
    [Header("Advanced")]
    public bool useDynamicTimeWarping = false;
    public bool normalizeByVoiceRange = true;
}