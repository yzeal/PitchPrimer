using UnityEngine;

// COPILOT CONTEXT: Speech segment data structure for rhythm analysis
// Used by ScoringManager to analyze speaking patterns and timing

[System.Serializable]
public struct SpeechSegment
{
    public float start;
    public float duration;
    
    public float End => start + duration;
    
    public override string ToString()
    {
        return $"SpeechSegment: {start:F2}s - {End:F2}s ({duration:F2}s)";
    }
}